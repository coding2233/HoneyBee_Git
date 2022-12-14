using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using ImGuiNET;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using strange.extensions.context.api;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wanderer.Common;
using Wanderer.GitRepository.Common;

namespace Wanderer.GitRepository.View
{
    public class GitCommandView : ImGuiView
    {
        private static GitCommandView s_gitCommandView;
        private IGitCommand m_gitCommand;
        private bool m_open;

        //public static List<ViewCommand> ViewCommands { get; internal set; } = new List<ViewCommand>();
        public GitCommandView(IContext context) : base(context)
        {
            s_gitCommandView = this;
        }

        public override void OnDraw()
        {
            if (m_gitCommand != null)
            {
                //var size = ImGui.GetWindowSize() * 0.3f;
                //var pos = ImGui.GetWindowSize() * 0.5f;
                var viewport = ImGui.GetMainViewport();
                ImGui.OpenPopup(m_gitCommand.Name);
                ImGui.SetNextWindowSize(viewport.WorkSize * 0.35f);
                //ImGui.SetNextWindowPos(pos);
                bool openCommandDraw = true;
                if (ImGui.BeginPopupModal(m_gitCommand.Name, ref m_open, ImGuiWindowFlags.NoResize))
                {
                    openCommandDraw = m_gitCommand.Draw();
                }

                openCommandDraw = openCommandDraw && m_open;
                if (!openCommandDraw)
                {
                    m_open = false;
                    m_gitCommand.Dispose();
                    m_gitCommand = null;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        private void SetGitCommand<T>(params object[] pArgs)  where T : IGitCommand
        {
            if (m_gitCommand != null)
            {
                Log.Warn("Command {0} is runing.", m_gitCommand.Name);
                return;
            }
            m_open = true;
            m_gitCommand = (T)Activator.CreateInstance(typeof(T),pArgs);
        }

        public static void RunGitCommandView<T>(params object[] pArgs) where T : IGitCommand
        {
            if (s_gitCommandView != null)
            {
               s_gitCommandView.SetGitCommand<T>(pArgs);
            }
        }

        public static void ShowTerminal(string repoPath)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "where";
            startInfo.Arguments = "git-bash";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            var process = Process.Start(startInfo);
            process.WaitForExit();
            string readLine = process.StandardOutput.ReadLine();
            if (!string.IsNullOrEmpty(readLine) && readLine.Contains("git-bash"))
            {
                Log.Info("where git-bash : {0}", readLine);

                Process.Start(readLine, $"--cd={Path.Combine(repoPath, "../")}");
            }
            else
            {
                Log.Info("下载Git https://github.com/git-for-windows/git/releases/download/v2.38.0.windows.1/MinGit-2.38.0-64-bit.zip");
            }
        }

    }

    public interface IGitCommand:IDisposable
    {
        string Name { get; }
        bool Draw();
    }

    public class PushGitCommand : ProcessGitCommand
    {
        public override  string Name => "Git Push";
        protected bool m_pushTags;
        protected bool m_pushForce;

        public PushGitCommand(GitRepo gitRepo):base(gitRepo)
        {
        }

        protected override bool OnDrawExecute()
        {
            ImGui.Text("Push the local branch to the remote repository");
            ImGui.Combo("Branch", ref m_localBranchIndex, m_localBranchNames, m_localBranchNames.Length);
            ImGui.Combo("Remote", ref m_remoteBranchIndex, m_remoteBranchNames, m_remoteBranchNames.Length);
            ImGui.Checkbox("Force push", ref m_pushForce);
            ImGui.Checkbox("Push all tags", ref m_pushTags);
            ImGui.SetCursorPosX(ImGui.GetWindowSize().X * 0.55f);
            if (ImGui.Button("Push"))
            {
                string localBranch = m_localBranchNames[m_localBranchIndex];
                string remoteBranch = m_remoteBranchNames[m_remoteBranchIndex];
                int remoteIndex = remoteBranch.IndexOf("/");
                string remote = remoteBranch.Substring(0, remoteIndex);
                remoteIndex++;
                remoteBranch = remoteBranch.Substring(remoteIndex, remoteBranch.Length - remoteIndex);
                string arguments = "push";
                if (m_pushForce)
                {
                    arguments += " --force";
                }
                if (m_pushTags)
                {
                    arguments += " --tags";
                }
                arguments += $" {remote} {localBranch}:{remoteBranch}";

                Log.Info(arguments);
                Execute(arguments);
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                return false;
            }
            return true;
        }

      
    }

    public class PullGitCommand : ProcessGitCommand
    {
        public override  string Name => "Git Pull";

        public PullGitCommand(GitRepo gitRepo) : base(gitRepo)
        {
           string arguments = $"pull";
            Log.Info(arguments);
            Execute(arguments);
        }

    }

    public class FetchGitCommand : ProcessGitCommand
    {
        public override string Name => "Git Fetch";

        private bool m_fetchAll;
        private bool m_force;

        public FetchGitCommand(GitRepo gitRepo) : base(gitRepo)
        {
        }

        protected override bool OnDrawExecute()
        {
            ImGui.Text("Fetch the remote repository to the local branch");
            if (m_fetchAll)
            {
                ImGui.BeginDisabled();
            }
            ImGui.Combo("Remote", ref m_remoteBranchIndex, m_remoteBranchNames, m_remoteBranchNames.Length);
            ImGui.Combo("Branch", ref m_localBranchIndex, m_localBranchNames, m_localBranchNames.Length);
            ImGui.Checkbox("Force", ref m_force);
            if (m_fetchAll)
            {
                ImGui.EndDisabled();
            }
            ImGui.Checkbox("Fetch all", ref m_fetchAll);
            ImGui.SetCursorPosX(ImGui.GetWindowSize().X * 0.55f);
            if (ImGui.Button("Fetch"))
            {
                string arguments = "fetch";
                if (m_fetchAll)
                {
                    arguments += " --all";
                }
                else
                {
                    string localBranch = m_localBranchNames[m_localBranchIndex];
                    string remoteBranch = m_remoteBranchNames[m_remoteBranchIndex];
                    int remoteIndex = remoteBranch.IndexOf("/");
                    string remote = remoteBranch.Substring(0, remoteIndex);
                    remoteIndex++;
                    remoteBranch = remoteBranch.Substring(remoteIndex, remoteBranch.Length - remoteIndex);
                    if (m_force)
                    {
                        arguments += " --force";
                    }
                    arguments += $" {remote} {remoteBranch}:{localBranch}";
                }
                Log.Info(arguments);
                Execute(arguments);
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                return false;
            }
            return true;
        }


    }

    public class ProcessGitCommand : IGitCommand
    {
        public virtual string Name { get; protected set; } = "Process Command";
        protected List<string> m_lines = new List<string>();
        protected CancellationTokenSource m_cancellationTokenSource;
        protected bool m_executed;
        protected bool m_exit;
        protected GitRepo m_gitRepo;

        protected string[] m_localBranchNames;
        protected int m_localBranchIndex;

        protected string[] m_remoteBranchNames;
        protected int m_remoteBranchIndex;



        public ProcessGitCommand(GitRepo gitRepo)
        {
            m_gitRepo = gitRepo;
            m_exit = false;
            m_executed = false;

            List<string> remoteBranchs = new List<string>();
            List<string> localBranchs = new List<string>();
            foreach (var item in gitRepo.Repo.Branches)
            {
                if (item.IsRemote)
                {
                    if (item.CanonicalName.EndsWith("/HEAD"))
                    {
                        continue;
                    }
                    remoteBranchs.Add(item.FriendlyName);
                }
                else
                {
                    localBranchs.Add(item.FriendlyName);
                }
            }

            m_localBranchNames = localBranchs.ToArray();
            m_remoteBranchNames = remoteBranchs.ToArray();
        }

        protected void AddNewLine(string line)
        {
            if (!string.IsNullOrEmpty(line))
            {
                m_lines.Add(line);
                Log.Info(line);
            }
        }

        protected async void Execute(string arguments,string cliTarget="git")
        {
            m_cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = default(CancellationToken);
            cancellationToken = m_cancellationTokenSource.Token;
            try
            {
                m_executed = true;
                AddNewLine(m_gitRepo.RootPath);
                AddNewLine("$ "+ cliTarget +" " + arguments);
                var result = await Cli.Wrap(cliTarget)
               .WithArguments(arguments)
               .WithWorkingDirectory(m_gitRepo.RootPath)
               .WithStandardOutputPipe(PipeTarget.ToDelegate(AddNewLine, Encoding.UTF8))
               .WithStandardErrorPipe(PipeTarget.ToDelegate(AddNewLine, Encoding.UTF8))
               .WithValidation(CommandResultValidation.None)
               .ExecuteAsync(cancellationToken);

                AddNewLine($"{result.ExitTime} ExitCode {result.ExitCode}");
            }
            catch (Exception e)
            {
                AddNewLine($"git {arguments} exception: {e}");
            }
            finally
            {
                if (m_cancellationTokenSource != null)
                {
                    m_cancellationTokenSource.Dispose();
                    m_cancellationTokenSource = null;
                }
                m_exit = true;
            }
        }

        protected virtual bool OnDrawOutput()
        {
            if (m_lines.Count > 0)
            {
                ImGui.BeginGroup();
                foreach (var item in m_lines)
                {
                    ImGui.Text(item);
                }
                ImGui.EndGroup();
            }

            if (!m_exit)
            {
                if (ImGui.Button("Cancel"))
                {
                    if (m_cancellationTokenSource != null)
                    {
                        m_cancellationTokenSource.Cancel(true);
                    }
                    m_exit = true;
                    return false;
                }
            }
            else
            {
                if (ImGui.Button("Close"))
                {
                    return false;
                }
            }
            return true;
        }

        protected virtual bool OnDrawExecute()
        {
            return true;
        }

        public virtual bool Draw()
        {
            try
            {
                if (m_executed)
                {
                    if (!OnDrawOutput())
                    {
                        return false;
                    }
                }
                else
                {
                    if (!OnDrawExecute())
                    {
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("ProcessGitCommand Draw exception: {0}",e);
                return false;
            }
            return true;
        }

      

        public virtual void Dispose()
        {
            if (m_cancellationTokenSource != null)
            {
                m_cancellationTokenSource.Dispose();
                m_cancellationTokenSource = null;
            }
        }

    }

    //public class LuaProcessGitCommand : ProcessGitCommand
    //{
    //    public override string Name { get; protected set; } = "Git Common Command - Lua";

    //    //public LuaProcessGitCommand(GitRepo gitRepo, ViewCommand command) : base(gitRepo)
    //    //{
    //    //    Name = command.Action;
    //    //    string action = gitRepo.FormatCommandAction(command);
    //    //    Log.Info(action);

    //    //    if (action.StartsWith("git"))
    //    //    {
    //    //        string arguments = action.Substring(3, action.Length - 3).Trim();
    //    //        Execute(arguments);
    //    //    }
    //    //    else
    //    //    {
    //    //        Log.Warn("Non-git commands");
    //    //        var args = action.Split(' ');
    //    //        string target = args[0];
    //    //        string arguments = action.Substring(target.Length, action.Length - target.Length).Trim();
    //    //        Execute(arguments, target);
    //    //    }
    //    //}
    //}

}
