using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using ImGuiNET;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using SixLabors.ImageSharp.PixelFormats;
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

namespace Wanderer.GitRepository.View
{
    public class GitCommandView : ImGuiView
    {
        private static GitCommandView s_gitCommandView;
        private IGitCommand m_gitCommand;

        public GitCommandView(IContext context) : base(context)
        {
            s_gitCommandView = this;
        }

        public override void OnDraw()
        {
            if (m_gitCommand != null)
            {
                ImGui.OpenPopup(m_gitCommand.Name);
                ImGui.BeginPopupModal(m_gitCommand.Name);
                if (!m_gitCommand.Draw())
                {
                    m_gitCommand.Dispose();
                    m_gitCommand = null;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        private T SetGitCommand<T>(params object[] pArgs)  where T : IGitCommand
        {
            if (m_gitCommand != null)
            {
                return default(T);
            }

            m_gitCommand = (T)Activator.CreateInstance(typeof(T),pArgs);
            return (T)m_gitCommand;
        }

        public static T RunGitCommandView<T>(params object[] pArgs) where T : IGitCommand
        {
            if (s_gitCommandView != null)
            {
               return  s_gitCommandView.SetGitCommand<T>(pArgs);
            }
            return default(T);
        }

        public static void ShowTerminal(string repoPath)
        {
            var gitbashPath = new ProcessGitCommand().WaitForExit("where","git-bash");
            if (!string.IsNullOrEmpty(gitbashPath) && gitbashPath.Contains("git-bash"))
            {
                Log.Info("where git-bash : {0}", gitbashPath);

                Process.Start(gitbashPath, $"--cd={Path.Combine(repoPath, "../")}");
            }
            else
            {
                Log.Info("下载Git https://github.com/git-for-windows/git/releases/download/v2.38.0.windows.1/MinGit-2.38.0-64-bit.zip");
            }
        }

        public static void PushCmd(string repoPath)
        {
           var cmd = RunGitCommandView<PushGitCommand>(repoPath);
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

        private List<string> m_lines = new List<string>();
        private CancellationTokenSource m_cancellationTokenSource;

        private bool m_exit;

        public PushGitCommand(string repoPath)
        {
            m_exit = false;
            m_cancellationTokenSource = new CancellationTokenSource();
            Exec(repoPath, m_cancellationTokenSource);
        }

        private async void Exec(string dir, CancellationTokenSource cts)
        {
            CancellationToken cancellationToken = default(CancellationToken);
            if (cts != null)
            {
                cancellationToken = cts.Token;
            }
            var result = await Cli.Wrap("git")
           .WithArguments("push")
           .WithWorkingDirectory(dir)
           .WithStandardOutputPipe(PipeTarget.ToDelegate((line) => {
               m_lines.Add($"out> {line}");
           }, Encoding.UTF8))
           .WithStandardErrorPipe(PipeTarget.ToDelegate((line) => { 
               m_lines.Add($"err> {line}");
           }, Encoding.UTF8))
           .WithValidation(CommandResultValidation.None)
           .ExecuteAsync(cancellationToken);

            m_lines.Add(result.ExitCode.ToString());
            m_lines.Add(result.ExitTime.ToString());
            //m_runing = false;
        }

        public override bool Draw()
        {
            if (ImGui.Button("Cancel"))
            {
                if (m_cancellationTokenSource != null)
                {
                    m_cancellationTokenSource.Cancel();
                }
                m_exit = true;
            }

            if (m_lines.Count > 0)
            {
                ImGui.BeginGroup();
                foreach (var item in m_lines)
                {
                    ImGui.Text(item);
                }
                ImGui.EndGroup();
            }

            return !m_exit;
        }
    }

    public class ProcessGitCommand : IGitCommand
    {
        public virtual string Name => "Process Command";
        public ProcessGitCommand()
        {
        
        }

        public virtual bool Draw()
        {
            return true;
        }

        public Process Run(string fileName, string arguments,string workDir=null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            if (!string.IsNullOrEmpty(workDir))
            {
                startInfo.WorkingDirectory = workDir;
            }
            startInfo.FileName = fileName;
            startInfo.Arguments = arguments;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            return Process.Start(startInfo);
        }

        public string WaitForExit(string fileName, string arguments, string workDir = null)
        {
            var process = Run(fileName, arguments, workDir);
            process.WaitForExit();
            string readLine = process.StandardOutput.ReadLine();
            process.Kill();
            process.Dispose();
            return readLine;
        }

      

        public void Dispose()
        {
        }

    }

}
