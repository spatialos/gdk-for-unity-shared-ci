using CommandLine;
using System;
using System.IO;
using System.Text;
using Octokit;

namespace ReleaseTool
{
    /// <summary>
    /// Runs the commands required for releasing a candidate.
    /// * Does a final bump of the gdk.pinned (if needed).
    /// * Merges the candidate branch into develop.
    /// * Pushes develop to origin/develop and origin/master.
    /// * Creates a GitHub release draft.
    /// </summary>
    internal class ReleaseCommand
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        
        private const string UpdateGdkCommitMessageTemplate = "Update to release commit for Gdk {0}.";
        private const string MergeCommitMessageTemplate = "Merge release {0}.";

        private const string PackageContentType = "application/zip";
        private const string ChangeLogFilename = "CHANGELOG.md";

        [Verb("release", HelpText = "Merge a release branch and create a github release draft.")]
        public class Options: GitClient.IGitOptions, GitHubClient.IGitHubOptions
        {
            [Value(0, MetaName = "version", HelpText = "The version that is being released.")]
            public string Version { get; set; }

            [Option('g', "update-gdk", HelpText = "The git hash of the version of the gdk to upgrade to. (Only if this is a project).")]
            public string GdkVersion { get; set; }

            public string GitRepoName { get; set; }

            [Option('u', "unattended", HelpText = "Whether to run in unattended mode.")]
            public bool IsUnattended { get; set; }

            public string GitRemote { get; set; }

            public string DevBranch { get; set; }

            public string MasterBranch { get; set; }
            
            public string GithubUser { get; set; }

            public string GitHubTokenFile { get; set; }
            
            public string GitHubToken { get; set; }
        }

        private readonly Options options;

        public ReleaseCommand(Options options)
        {
            this.options = options;
        }

        public int Run()
        {
            var gitClient = new GitClient(options);
            var gitHubClient = new GitHubClient(options);

            try
            {
                if (gitClient.HasStagedOrModifiedFiles())
                {
                    throw new InvalidOperationException("The repository currently has files staged, please stash, reset or commit them.");
                }

                gitHubClient.LoadCredentials();
                var gitHubRepo = gitHubClient.GetRepositoryFromRemote(gitClient.GetRemoteUrl());

                // Checkout "origin/feature/release-X"
                var branchName = GetBranchName();
                gitClient.Fetch();
                gitClient.CheckoutRemoteBranch(branchName);

                // Make Changes if necessary
                UpdateGdkVersion(gitClient);

                // Merge into develop
                Logger.Info("Merging {0} into {1}...", branchName, options.DevBranch);
                var branchCommit = gitClient.GetHeadCommit();
                gitClient.CheckoutRemoteBranch(options.DevBranch);
                gitClient.SquashMerge(branchCommit, string.Format(MergeCommitMessageTemplate, options.Version));

                // Push to develop
                Logger.Info("Pushing to {0}...", options.DevBranch);
                var devHasBranchEnforcement = gitHubClient.RemoveAdminBranchEnforcement(gitHubRepo, options.DevBranch);
                try
                {
                    gitClient.Push(options.DevBranch);
                }
                finally
                {
                    if (devHasBranchEnforcement)
                    {
                        Logger.Info("Re-adding branch protection...");
                        gitHubClient.AddAdminBranchEnforcement(gitHubRepo, options.DevBranch);
                    }
                }

                // Push to master
                Logger.Info("Pushing to {0}...", options.MasterBranch);
                Logger.Info("Removing branch protection...");
                var masterHasBranchEnforcement = gitHubClient.RemoveAdminBranchEnforcement(gitHubRepo, options.MasterBranch);
                try
                {
                    gitClient.Push(options.MasterBranch);
                }
                finally
                {
                    if (masterHasBranchEnforcement)
                    {
                        Logger.Info("Re-adding branch protection...");
                        gitHubClient.AddAdminBranchEnforcement(gitHubRepo, options.MasterBranch);
                    }
                }
                
                // Create release
                var release = CreateRelease(gitHubClient, gitHubRepo, gitClient);

                Logger.Info("Release Successful!");
                Logger.Info("Release hash: {0}", gitClient.GetHeadCommit().Sha);
                Logger.Info("Draft release: {0}", release.HtmlUrl);

                if (!options.IsUnattended)
                {
                    System.Diagnostics.Process.Start(release.HtmlUrl);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "ERROR: Unable to release candidate branch. Error: {0}", e);
                return 1;
            }
            finally
            {
                gitClient.Dispose();
            }

            return 0;
        }

        private string GetBranchName()
        {
            return CommandsCommon.GetReleaseBranchName(options.Version);
        }

        private void UpdateGdkVersion(GitClient gitClient)
        {
            if (!ShouldUpdateGdkVersion())
            {
                return;
            }

            Logger.Info("Updating gdk version, {0}...", CommandsCommon.GdkPinnedFilename);
            
            CommandsCommon.UpdateGdkVersion(gitClient, options.GdkVersion);

            gitClient.Commit(string.Format(UpdateGdkCommitMessageTemplate, options.Version));
        }

        private bool ShouldUpdateGdkVersion()
        {
            return !string.IsNullOrEmpty(options.GdkVersion);
        }

        private Release CreateRelease(GitHubClient gitHubClient, Octokit.Repository gitHubRepo, GitClient gitClient)
        {
            Logger.Info("Running packer...");
            
            var package = Packer.Program.Package(Environment.CurrentDirectory);

            var commitish = gitClient.GetHeadCommit().Sha;
            
            var releaseBody = GetReleaseNotesFromChangeLog();
            var release = gitHubClient.CreateDraftRelease(gitHubRepo, options.Version, releaseBody, options.Version,
                commitish);

            using (var reader = File.OpenRead(package))
            {
                gitHubClient.AddAssetToRelease(release, Path.GetFileName(package), PackageContentType, reader);
            }

            return release;
        }

        private static string GetReleaseNotesFromChangeLog()
        {
            if (!File.Exists(ChangeLogFilename))
            {
                throw new InvalidOperationException("Could not get draft release notes, as the change log file, " +
                                                    $"{ChangeLogFilename}, does not exist.");
            }

            Logger.Info("Reading {0}...", ChangeLogFilename);

            var releaseBody = new StringBuilder();
            var changedSection = 0;

            using (var reader = new StreamReader(ChangeLogFilename))
            {
                while (!reader.EndOfStream)
                {
                    // Here we target the second Heading2 ("##") section.
                    // The first section will be the "Unreleased" section. The second will be the correct release notes.
                    var line = reader.ReadLine();
                    if (line.StartsWith("## "))
                    {
                        changedSection += 1;

                        if (changedSection == 3)
                        {
                            break;
                        }

                        continue;
                    }

                    if (changedSection == 2)
                    {
                        releaseBody.AppendLine(line);
                    }
                }
            }

            return releaseBody.ToString();
        }
    }
}
