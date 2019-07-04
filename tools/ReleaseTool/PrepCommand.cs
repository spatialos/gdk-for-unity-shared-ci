using System;
using System.IO;
using System.Text;
using CommandLine;
using Newtonsoft.Json.Linq;
using Packer;

namespace ReleaseTool
{
    /// <summary>
    ///     Runs the steps required to cut a release candidate branch.
    ///     * Alters all package.json.
    ///     * Bumps the version in the CHANGELOG.md.
    ///     * Updates the hash in the gdk.pinned file.
    ///     * Pushes the candidate and creates a Pull Request against develop.
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
        private const string ReleaseBranchNameTemplate = "feature/release-{0}";

        private const string PullRequestTemplate = "Release {0} - Pre-Validation";

        [Verb("prep", HelpText = "Prep a release candidate branch.")]
        public class Options : GitHubClient.IGitHubOptions
        {
            [Value(0, MetaName = "version", HelpText = "The release version that is being cut.", Required = true)]
            public string Version { get; set; }

            [Option('g', "update-pinned-gdk", HelpText =
                "The git hash of the version of the gdk to upgrade to. (Only if this is a project).")]
            public string PinnedGdkVersion { get; set; }

            // TODO: Once we have a robots account - set this to default robot account.
            [Option("github-user", HelpText = "The user account that is using this.", Required = true)]
            public string GithubUser { get; set; }

            [Option("git-repository-name", HelpText = "The Git repository that we are targeting.", Required = true)]
            public string GitRepoName { get; set; }

            #region IGithubOptions implementation

            public string GitHubTokenFile { get; set; }

            public string GitHubToken { get; set; }

            #endregion
        }

        private readonly Options options;

        public PrepCommand(Options options)
        {
            this.options = options;
        }

        /*
         *     This tool is designed to be used with a robot Github account which has its own fork of the GDK
         *     repositories. This means that when we prep a release:
         *         1. Checkout our fork of the repo.
         *         2. Add the spatialos org remote to our local copy and fetch this remote.
         *         3. Checkout the spatialos/develop branch (the non-forked develop branch).
         *         4. Make the changes for prepping the release.
         *         5. Push this to an RC branch on the forked repository.
         *         6. Open a PR from the fork into the source repository.
         */
        public int Run()
        {
            var remoteUrl = string.Format(Common.RemoteUrlTemplate, options.GithubUser, options.GitRepoName);

            try
            {
                var gitHubClient = new GitHubClient(options);

                using (var gitClient = GitClient.FromRemote(remoteUrl))
                {
                    // This does step 2 from above.
                    var spatialOsRemote =
                        string.Format(Common.RemoteUrlTemplate, Common.SpatialOsOrg, options.GitRepoName);
                    gitClient.AddRemote(Common.SpatialOsOrg, spatialOsRemote);
                    gitClient.Fetch(Common.SpatialOsOrg);

                    // This does step 3 from above.
                    gitClient.CheckoutRemoteBranch(Common.DevelopBranch, Common.SpatialOsOrg);

                    // This does step 4 from above.
                    using (new WorkingDirectoryScope(gitClient.RepositoryPath))
                    {
                        UpdateAllPackageJsons(gitClient);
                        UpdateGdkVersion(gitClient);
                        UpdateChangeLog(gitClient);
                        UpdatePackerConfig(gitClient);
                    }

                    // This does step 5 from above.
                    var branchName = string.Format(ReleaseBranchNameTemplate, options.Version);
                    gitClient.Commit(string.Format(CommitMessageTemplate, options.Version));
                    gitClient.Push(branchName);

                    // This does step 6 from above.
                    var gitHubRepo = gitHubClient.GetRepositoryFromRemote(spatialOsRemote);
                    var pullRequest = gitHubClient.CreatePullRequest(gitHubRepo,
                        $"{options.GithubUser}:{branchName}", Common.DevelopBranch,
                        string.Format(PullRequestTemplate, options.Version));

                    Logger.Info("Successfully created release!");
                    Logger.Info("Release hash: {0}", gitClient.GetHeadCommit().Sha);
                    Logger.Info("Pull request available: {0}", pullRequest.HtmlUrl);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "ERROR: Unable to prep release candidate branch. Error: {0}", e);
                return 1;
            }

            return 0;
        }

        private void UpdateAllPackageJsons(GitClient gitClient)
        {
            Logger.Info("Updating all {0}...", PackageJsonFilename);

            var packageFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), PackageJsonFilename,
                SearchOption.AllDirectories);

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

                    foreach (var property in dependencies.Properties())
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

            UpdateGdkVersion(gitClient, options.PinnedGdkVersion);
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

            using (var reader = new StreamReader(ChangeLogFilename))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();

                    if (line.StartsWith("## ") && !hasReplacedUnreleased)
                    {
                        newChangeLog.AppendLine(line);
                        newChangeLog.AppendLine(string.Empty);
                        newChangeLog.AppendLine(string.Format(ChangeLogReleaseHeadingTemplate, options.Version,
                            DateTime.Now));

                        hasReplacedUnreleased = true;
                        continue;
                    }

                    if (hasReplacedUnreleased && shouldUpdateGdkVersion && line.StartsWith("### Changed"))
                    {
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

        private bool ShouldUpdateGdkVersion()
        {
            return !string.IsNullOrEmpty(options.PinnedGdkVersion);
        }

        private static void UpdateGdkVersion(GitClient gitClient, string newPinnedVersion)
        {
            const string gdkPinnedFilename = "gdk.pinned";

            Logger.Info("Updating pinned gdk version to {0}...", newPinnedVersion);

            if (!File.Exists(gdkPinnedFilename))
            {
                throw new InvalidOperationException("Could not upgrade gdk version as the file, " +
                    $"{gdkPinnedFilename}, does not exist");
            }

            File.WriteAllText(gdkPinnedFilename, newPinnedVersion);

            gitClient.StageFile(gdkPinnedFilename);
        }
    }
}
