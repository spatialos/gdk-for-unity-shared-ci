using System;
using System.Diagnostics;
using System.IO;
using CommandLine;
using LibGit2Sharp;

namespace ReleaseTool
{
    /// <summary>
    /// This class provides helper methods for git. It uses a hybrid approach using both LibGit2Sharp for most methods,
    /// but uses Processes to invoke remote methods (pull, fetch, push). This is because LibGit2Sharp does not support
    /// ssh authentication yet!
    /// </summary>
    internal class GitClient : IDisposable
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        
        private const string DefaultRemote = "origin";
        private const string DefaultDevBranch = "develop";
        private const string DefaultMasterBranch = "master";

        private const string GitCommand = "git";
        private const string PushArgumentsTemplate = "push {0} HEAD:refs/heads/{1}";
        private const string FetchArguments = "fetch";
        private const string PushForceFlag = " -f";
        private const string SquashMergeArgumentsTemplate = "merge --squash {0} -m \"{1}\"";

        private const string RemoteBranchRefTemplate = "{1}/{0}";

        public interface IGitOptions
        {            
            [Option("git-remote", Default = DefaultRemote, HelpText = "The git remote to push branches to.")]
            string GitRemote { get; set; }

            [Option("dev-branch", Default = DefaultDevBranch, HelpText = "The development branch.")]
            string DevBranch { get; set; }

            [Option("master-branch", Default = DefaultMasterBranch, HelpText = "The master branch.")]
            string MasterBranch { get; set; }
            
            bool IsUnattended { get; set; }
        }

        private readonly IGitOptions options;
        private readonly Uri repoDirectory;
        private readonly IRepository repo;

        public GitClient(IGitOptions options)
        {
            this.options = options;
            repoDirectory = new Uri(Common.AppendDirectorySeparator(Directory.GetCurrentDirectory()), UriKind.Absolute);
            repo = new Repository(repoDirectory.AbsolutePath);
        }

        public void Dispose()
        {
            repo?.Dispose();
        }

        public void Checkout(Branch branch)
        {
            Logger.Info("Checking out branch... {0}", branch.FriendlyName);
            Commands.Checkout(repo, branch);
        }

        public void CheckoutRemoteBranch(string branch)
        {
            var branchRef = string.Format(RemoteBranchRefTemplate, branch, options.GitRemote);
            Logger.Info("Checking out branch... {0}", branchRef);
            Commands.Checkout(repo, branchRef);
        }

        public bool HasStagedOrModifiedFiles()
        {
            var repoStatus = repo.RetrieveStatus();
            return repoStatus.IsDirty;
        }

        public void StageFile(string fileName, UriKind uriKind)
        {
            Logger.Info("Staging... {0}", fileName);
            
            var fileUri = new Uri(fileName, uriKind);

            if (fileUri.IsAbsoluteUri)
            {
                fileUri = repoDirectory.MakeRelativeUri(fileUri);
            }
            
            Commands.Stage(repo, fileUri.ToString());
        }

        public void Commit(string commitMessage)
        {
            Logger.Info("Committing...");

            var signature = repo.Config.BuildSignature(DateTimeOffset.Now);
            repo.Commit(commitMessage, signature, signature, new CommitOptions { AllowEmptyCommit = true });
        }

        public void Fetch()
        {
            Logger.Info("Fetching from remote...");

            RunGitCommand("fetch", FetchArguments);
        }

        public void Push(string remoteBranchName)
        {
            Logger.Info("Pushing to remote...");

            var pushArguments = string.Format(PushArgumentsTemplate, options.GitRemote, remoteBranchName);

            RunGitCommand("push branch", pushArguments);
        }

        public void ForcePush(string remoteBranchName)
        {
            Logger.Info("Force Pushing to remote...");

            var pushArguments = string.Format(PushArgumentsTemplate, options.GitRemote, remoteBranchName);
            pushArguments += PushForceFlag;

            RunGitCommand("force push branch", pushArguments);
        }

        public void SquashMerge(Commit commit, string mergeCommitMessage)
        {
            Logger.Info("Performing squash merge...");

            var squashMergeArguments = string.Format(SquashMergeArgumentsTemplate, commit.Sha,
                mergeCommitMessage);
            
            RunGitCommand("squash merge", squashMergeArguments);
        }
        
        private void RunGitCommand(string description, string arguments)
        {
            while (true)
            {
                Logger.Debug("Attempting to {0}. Running command [{1} {2}]", description, 
                    GitCommand, arguments);

                var process = Process.Start(new ProcessStartInfo(GitCommand, arguments)
                {
                    UseShellExecute = false
                });

                process.WaitForExit();

                Console.WriteLine(string.Empty);

                if (process.ExitCode == 0)
                {
                    break;
                }

                if (options.IsUnattended)
                {
                    throw new InvalidOperationException($"Failed to {description}.");
                }
 
                var shouldRetry = false;

                while (true)
                {
                    Logger.Info($"Failed to {description}. Retry (y/n)?");
                    var retryInput = Console.ReadLine().ToLower();

                    if (retryInput == "y")
                    {
                        shouldRetry = true;
                        break;
                    }

                    if (retryInput == "n")
                    {
                        break;
                    }
                }

                if (!shouldRetry)
                {
                    throw new InvalidOperationException($"Failed to {description}.");
                }
            }
        }

        public string GetRemoteUrl()
        {
            return repo.Network.Remotes[options.GitRemote].PushUrl;
        }

        public Commit GetHeadCommit()
        {
            return repo.Head.Tip;
        }
    }
}
