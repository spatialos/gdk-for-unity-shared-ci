using CommandLine;
using Newtonsoft.Json.Linq;
using Octokit;
using System;
using System.IO;
using System.Text;
using Packer;

namespace ReleaseTool
{
    /// <summary>
    /// Runs the steps required to cut a release candidate branch.
    /// * Alters all package.json.
    /// * Bumps the version in the CHANGELOG.md.
    /// * Updates the hash in the gdk.pinned file.
    /// * Pushes the candidate and creates a Pull Request against develop.
    /// </summary>
    internal class PrepCommand
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        
        private const string PackerConfigFile = "packer.config.json";
        
        private const string PackageJsonFilename = "package.json";
        private const string PackageJsonVersionString = "version";
        private const string PackageJsonDependenciesString = "dependencies";
        private const string PackageJsonDependenciesPrefix = "com.improbable.gdk";

        private const string CommitMessageTemplate = "Release candidate for version {0}.";

        private const string ChangeLogFilename = "CHANGELOG.md";
        private const string ChangeLogReleaseHeadingTemplate = "## `{0}` - {1:yyyy-MM-dd}";
        private const string ChangeLogUpdateGdkTemplate = "- Upgraded to GDK for Unity version `{0}`";

        private const string PullRequestTemplate = "Release {0} - Pre-Validation";
        
        [Verb("prep", HelpText = "Prep a release candidate branch.")]
        public class Options: GitClient.IGitOptions, GitHubClient.IGitHubOptions
        {
            [Value(0, MetaName = "version", HelpText = "The release version that is being cut.", Required = true)]
            public string Version { get; set; }

            [Option('g', "update-gdk", HelpText = "The git hash of the version of the gdk to upgrade to. (Only if this is a project).")]
            public string GdkVersion { get; set; }

            [Option('f', "force", HelpText = "Force create a new branch (delete the old if it exists).")]
            public bool Force { get; set; }

            [Option('d', "override-date", HelpText = "Override the date of the release. Leave blank to use the current date.")]
            public DateTime? OverrideDate { get; set; }

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

        public PrepCommand(Options options)
        {
            this.options = options;
        }

        public int Run()
        {
            GitClient gitClient = null;
            
            try
            {
                var gitHubClient = new GitHubClient(options);
                gitHubClient.LoadCredentials();

                gitClient = new GitClient(options);

                // Checkout "spatialos:origin/develop"
                var spatialOSRemote = string.Format(Common.RemoteUrlTemplate, Common.SpatialOsOrg, options.GitRepoName);
                var gitHubRepo = gitHubClient.GetRepositoryFromRemote(spatialOSRemote);
                gitClient.AddRemote( Common.SpatialOsOrg, spatialOSRemote);
                gitClient.Fetch(Common.SpatialOsOrg);
                gitClient.CheckoutRemoteBranch(options.DevBranch, Common.SpatialOsOrg);

                // Create a new branch
                var branchName = BranchName();

                // Make Changes
                UpdateAllPackageJsons(gitClient);
                UpdateGdkVersion(gitClient);
                UpdateChangeLog(gitClient);
                UpdatePackerConfig(gitClient);

                // Push to origin
                gitClient.Commit(string.Format(CommitMessageTemplate, options.Version));
                if (options.Force)
                {
                    gitClient.ForcePush(branchName);
                }
                else
                {
                    gitClient.Push(branchName);
                }

                // Create pull request
                var pullRequest = gitHubClient.CreatePullRequest(gitHubRepo, $"{options.GithubUser}:{branchName}", options.DevBranch,
                    string.Format(PullRequestTemplate, options.Version));

                Logger.Info("Successfully created release!");
                Logger.Info("Release hash: {0}", gitClient.GetHeadCommit().Sha);
                Logger.Info("Pull request available: {0}", pullRequest.HtmlUrl);

                if (!options.IsUnattended)
                {
                    System.Diagnostics.Process.Start(pullRequest.HtmlUrl);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "ERROR: Unable to prep release candidate branch. Error: {0}", e);
                return 1;
            }
            finally
            {
                gitClient?.Dispose();
            }
            
            return 0;
        }

        private void UpdateAllPackageJsons(GitClient gitClient)
        {
            Logger.Info("Updating all {0}...", PackageJsonFilename);

            var packageFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), PackageJsonFilename, SearchOption.AllDirectories);

            foreach (var packageFile in packageFiles)
            {
                UpdatePackageJson(packageFile, gitClient);
            }
        }

        private void UpdatePackageJson(string packageFile, GitClient gitClient)
        {
            Logger.Info("Updating {0}...", packageFile);

            JObject jsonObject;
            using (var streamReader = new StreamReader(packageFile))
            {
                jsonObject = JObject.Parse(streamReader.ReadToEnd());
                if (jsonObject.ContainsKey(PackageJsonVersionString))
                {
                    jsonObject[PackageJsonVersionString] = options.Version;
                }

                if (jsonObject.ContainsKey(PackageJsonDependenciesString))
                {
                    var dependencies = (JObject) jsonObject[PackageJsonDependenciesString];

                    foreach(var property in dependencies.Properties())
                    {
                        if (property.Name.StartsWith(PackageJsonDependenciesPrefix))
                        {
                            dependencies[property.Name] = options.Version;
                        }
                    }
                }
            }

            File.WriteAllText(packageFile, jsonObject.ToString());
            
            gitClient.StageFile(packageFile);
        }

        private void UpdateGdkVersion(GitClient gitClient)
        {
            if (!ShouldUpdateGdkVersion())
            {
                return;
            }

            Logger.Info("Updating gdk version, {0}...", CommandsCommon.GdkPinnedFilename);
            
            CommandsCommon.UpdateGdkVersion(gitClient, options.GdkVersion);
        }

        /**
         * Simple method for editing the ChangeLog 
         */
        private void UpdateChangeLog(GitClient gitClient)
        {
            if (!File.Exists(ChangeLogFilename))
            {
                throw new InvalidOperationException("Could not update the change log as the file," +
                                                    $" {ChangeLogFilename}, does not exist");
            }

            Logger.Info("Updating {0}...", ChangeLogFilename);

            var newChangeLog = new StringBuilder();

            var hasReplacedUnreleased = false;
            var isInChangedSection = false;
            var isInChangedBulletPoints = false;
            var shouldUpdateGdkVersion = ShouldUpdateGdkVersion();

            var releaseDate = options.OverrideDate ?? DateTime.Now;

            using (var reader = new StreamReader(ChangeLogFilename))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();

                    if (line.StartsWith("## ") && !hasReplacedUnreleased)
                    {
                        newChangeLog.AppendLine(line);
                        newChangeLog.AppendLine(string.Empty);
                        newChangeLog.AppendLine(string.Format(ChangeLogReleaseHeadingTemplate, options.Version, releaseDate));

                        hasReplacedUnreleased = true;
                        continue;
                    }

                    if (hasReplacedUnreleased && shouldUpdateGdkVersion && line.StartsWith("### Changed")) {
                        newChangeLog.AppendLine(line);

                        isInChangedSection = true;
                        continue;
                    }

                    if (isInChangedSection && line.StartsWith("-"))
                    {
                        newChangeLog.AppendLine(line);

                        isInChangedBulletPoints = true;
                        continue;
                    }

                    if (isInChangedBulletPoints && !line.StartsWith("-"))
                    {
                        newChangeLog.AppendLine(string.Format(ChangeLogUpdateGdkTemplate, options.Version));
                        newChangeLog.AppendLine(line);

                        shouldUpdateGdkVersion = false;
                        isInChangedBulletPoints = false;
                        isInChangedSection = false;
                        continue;
                    }

                    // If there is no existing "Changed" section
                    if (shouldUpdateGdkVersion && line.StartsWith("## "))
                    {
                        newChangeLog.AppendLine("### Changed");
                        newChangeLog.AppendLine(string.Empty);
                        newChangeLog.AppendLine(string.Format(ChangeLogUpdateGdkTemplate, options.Version));
                        newChangeLog.AppendLine(string.Empty);
                        newChangeLog.AppendLine(line);

                        shouldUpdateGdkVersion = false;
                        continue;
                    }

                    newChangeLog.AppendLine(line);
                }
            }

            File.WriteAllText(ChangeLogFilename, newChangeLog.ToString());

            gitClient.StageFile(ChangeLogFilename);
        }

        private void UpdatePackerConfig(GitClient gitClient)
        {
            if (!File.Exists(PackerConfigFile))
            {
                return;
            }
            
            Logger.Info("Updating {0}...", PackerConfigFile);

            var config = ConfigModel.FromFile(PackerConfigFile);
            config.Version = options.Version;
            
            config.ToFile(PackerConfigFile);
            
            gitClient.StageFile(PackerConfigFile);
        }

        private string BranchName()
        {
            return CommandsCommon.GetReleaseBranchName(options.Version);
        }

        private bool ShouldUpdateGdkVersion()
        {
            return !string.IsNullOrEmpty(options.GdkVersion);
        }
    }
}
