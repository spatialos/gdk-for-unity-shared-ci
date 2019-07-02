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
        private const string FetchArguments = "fetch {0}";
        private const string SquashMergeArgumentsTemplate = "merge --squash {0} -m \"{1}\"";
        private const string CloneArgumentsTemplate = "clone {0} {1}";
        private const string AddRemoteArgumentsTemplate = "remote add {0} {1}";
        
        private const string RemoteBranchRefTemplate = "{1}/{0}";

        public interface IGitOptions
        {            
            [Option("git-remote", Default = DefaultRemote, HelpText = "The git remote to push branches to.")]
            string GitRemote { get; set; }

            [Option("dev-branch", Default = DefaultDevBranch, HelpText = "The development branch.")]
            string DevBranch { get; set; }

            [Option("master-branch", Default = DefaultMasterBranch, HelpText = "The master branch.")]
            string MasterBranch { get; set; }
            
            [Option("github-user", HelpText = "The user account that is using this.", Required = true)]
            string GithubUser { get; set; }

            [Option("git-repository-name", HelpText = "The Git repository that we are targeting.", Required = true)]
            string GitRepoName { get; set; }
        }

        public string RepositoryPath { get; }

        private readonly IGitOptions options;
        private readonly IRepository repo;

        public static GitClient CreateRepo(IGitOptions options)
        {
            var remoteUrl = string.Format(Common.RemoteUrlTemplate, options.GithubUser, options.GitRepoName);
            
            var repositoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(repositoryPath);
            Clone(remoteUrl, repositoryPath);
            
            return new GitClient(options, repositoryPath);
        }
        
        private GitClient(IGitOptions options, string repositoryPath)
        {
            this.options = options;
            RepositoryPath = repositoryPath;
            repo = new Repository($"{repositoryPath}/.git/");
        }

        public void Dispose()
        {
            repo?.Dispose();
        }

        public void CheckoutRemoteBranch(string branch, string remote = null)
        {
            var branchRef = string.Format(RemoteBranchRefTemplate, branch, remote ?? options.GitRemote);
            Logger.Info("Checking out branch... {0}", branchRef);
            Commands.Checkout(repo, branchRef);
        }

        public bool HasStagedOrModifiedFiles()
        {
            var repoStatus = repo.RetrieveStatus();
            return repoStatus.IsDirty;
        }

        public void StageFile(string filePath)
        {
            Logger.Info("Staging... {0}", filePath);

            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.Combine(RepositoryPath, filePath);
            }
            
            Commands.Stage(repo, filePath);
        }

        public void Commit(string commitMessage)
        {
            Logger.Info("Committing...");

            var signature = repo.Config.BuildSignature(DateTimeOffset.Now);
            repo.Commit(commitMessage, signature, signature, new CommitOptions { AllowEmptyCommit = true });
        }

        public void Fetch(string remote = null)
        {
            Logger.Info("Fetching from remote...");

            RunGitCommand("fetch", string.Format(FetchArguments, remote ?? options.GitRemote), RepositoryPath);
        }

        public void Push(string remoteBranchName)
        {
            Logger.Info("Pushing to remote...");

            var pushArguments = string.Format(PushArgumentsTemplate, options.GitRemote, remoteBranchName);

            RunGitCommand("push branch", pushArguments, RepositoryPath);
        }

        public void SquashMerge(Commit commit, string mergeCommitMessage)
        {
            Logger.Info("Performing squash merge...");

            var squashMergeArguments = string.Format(SquashMergeArgumentsTemplate, commit.Sha,
                mergeCommitMessage);
            
            RunGitCommand("squash merge", squashMergeArguments, RepositoryPath);
        }
        
        public void AddRemote(string name, string remoteUrl)
        {
            Logger.Info($"Adding remote {remoteUrl} as {name}...");
            RunGitCommand("add remote", string.Format(AddRemoteArgumentsTemplate, name, remoteUrl), RepositoryPath);
        }

        private static void Clone(string remoteUrl, string targetDirectory)
        {
            Logger.Info($"Cloning {remoteUrl} into {targetDirectory}...");
            RunGitCommand("clone repository", string.Format(CloneArgumentsTemplate, remoteUrl, $"\"{targetDirectory}\""));
        }

        private static void RunGitCommand(string description, string arguments, string workingDir = null)
        {
            Logger.Debug("Attempting to {0}. Running command [{1} {2}]", description, 
                GitCommand, arguments);

            var procInfo = new ProcessStartInfo(GitCommand, arguments)
            {
                UseShellExecute = false,
            };
            
            if (workingDir != null)
            {
                procInfo.WorkingDirectory = workingDir;
            }

            using (var process = Process.Start(procInfo))
            {
                if (process != null)
                {
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        return;
                    }
                }
            }

            throw new InvalidOperationException($"Failed to {description}.");
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
