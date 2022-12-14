using ImGuiNET;
using LibGit2Sharp;
using strange.extensions.dispatcher.eventdispatcher.api;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wanderer.App.Service;
using Wanderer.App.View;
using Wanderer.Common;
using Wanderer.GitRepository.Common;


namespace Wanderer.GitRepository.View
{
    internal class DrawCommitHistoryView
    {
        private GitRepo m_gitRepo;
        private int m_commitAddInterval = 5;
        private int m_commitViewIndex = 0;
        private int m_commitViewMax = 50;
        private float m_lastCommitScrollY = 0.0f;
        private Commit m_selectCommit;
        private Patch m_selectCommitPatch;
        private PatchEntryChanges m_selectCommitPatchEntry;

        private ShowDiffText m_showDiffText;
        private SplitView m_contentSplitView;

        private SplitView m_selectCommitDiffSpliteView;
        private SplitView m_selectCommitTreeSpliteView;

        private Range m_cacheRange;
        private IEnumerable<Commit> m_cacheCommits;

        private IPluginService m_plugin;

        public DrawCommitHistoryView(GitRepo gitRepo, IPluginService plugin)
        {
            m_contentSplitView = new SplitView(SplitView.SplitType.Vertical);
            m_selectCommitDiffSpliteView = new SplitView(SplitView.SplitType.Horizontal);
            m_selectCommitTreeSpliteView = new SplitView(SplitView.SplitType.Vertical);

            m_showDiffText = new ShowDiffText();

            m_gitRepo = gitRepo;
            m_plugin = plugin;
        }

        public void Draw()
        {
            m_contentSplitView.Begin();
            DrawHistoryCommits();
            m_contentSplitView.Separate();
            DrawSelectCommit();
            m_contentSplitView.End();
        }


        private void DrawHistoryCommits()
        {
            int commitMax = m_gitRepo.GetCommitCount();
            if (m_lastCommitScrollY <= 0.0f)
            {
                //float moveInterval = GetScrollInterval(_commitViewIndex - _commitAddInterval >= 0 ? _commitAddInterval : _commitViewIndex - _commitAddInterval);
                m_commitViewIndex -= m_commitAddInterval;
                m_commitViewIndex = Math.Max(m_commitViewIndex, 0);
                if (m_commitViewIndex > 0)
                    ImGui.SetScrollY(GetScrollInterval(m_commitAddInterval));
            }
            else if (m_lastCommitScrollY >= ImGui.GetScrollMaxY())
            {
                if (commitMax >= m_commitViewMax)
                {
                    m_commitViewIndex += m_commitAddInterval;
                    commitMax = commitMax - m_commitViewMax;
                    m_commitViewIndex = Math.Min(m_commitViewIndex, commitMax);
                }
                else
                {
                    m_commitViewIndex = 0;
                }

                if (m_commitViewIndex > 0 && m_commitViewIndex < commitMax)
                    ImGui.SetScrollY(ImGui.GetScrollMaxY() - GetScrollInterval(m_commitAddInterval));
            }
            m_lastCommitScrollY = ImGui.GetScrollY();

            var historyCommits = GetHistoryCommits();
            if (historyCommits == null)
                return;

            if (ImGui.BeginTable("GitRepo-Commits", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable))
            {
                //图谱
                ImGui.TableSetupColumn("Graph", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize);
                ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Date", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Author", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Commit", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize);
                ImGui.TableHeadersRow();

                List<CommitAtlasLine> commitAtlasLines = new List<CommitAtlasLine>();
                int atalsMaxId = -1;
                foreach (var item in historyCommits)
                {
                    //if (index < m_commitViewIndex)
                    //    continue;
                    //else if (index >= m_commitViewIndex + m_commitViewMax)
                    //    break;

                    //表格
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);

                    //图谱绘制
                    int atlasId = 0;
                    float pointXOffset = 0;
                    var atlasLines = commitAtlasLines.FindAll(x => x.Parent == item);
                    if (atlasLines != null && atlasLines.Count > 0)
                    {
                        foreach (var itemLine in atlasLines)
                        {
                            if (atlasId == 0)
                            {
                                atlasId = itemLine.AtlasId;
                            }
                            else
                            {
                                atlasId = Math.Min(atlasId, itemLine.AtlasId);
                            }
                            commitAtlasLines.Remove(itemLine);
                        }

                    }
                    else
                    {
                        atlasId = atalsMaxId + 1;
                    }
                    pointXOffset = ImGui.GetTextLineHeight() * atlasId;

                    var atlasPoint = ImGui.GetCursorPos() + ImGui.GetWindowPos() + new Vector2(pointXOffset, ImGui.GetTextLineHeight() * 0.5f - ImGui.GetScrollY());
                    ImGui.GetWindowDrawList().AddCircleFilled(atlasPoint, ImGui.GetTextLineHeight() * 0.25f, ImGui.GetColorU32(ImGuiCol.ButtonActive));
                    if (atlasLines != null && atlasLines.Count > 0)
                    {
                        foreach (var itemLine in atlasLines)
                        {
                            ImGui.GetWindowDrawList().AddLine(itemLine.ChildPoint, atlasPoint, ImGui.GetColorU32(ImGuiCol.ButtonActive));
                            Pool<CommitAtlasLine>.Release(itemLine);
                        }
                    }
              

                    if (item.Parents != null)
                    {
                        int itemIndex = 0;
                        foreach (var itemParent in item.Parents)
                        {
                            var atlasLine = Pool<CommitAtlasLine>.Get();
                            atlasLine.AtlasId = atlasId + itemIndex;
                            atlasLine.ChildPoint = atlasPoint;
                            atlasLine.Parent = itemParent;
                            itemIndex++;
                            commitAtlasLines.Add(atlasLine);

                            atalsMaxId = Math.Max(atalsMaxId, atlasLine.AtlasId);
                        }
                    }

                    //ImGui.Text("");
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + pointXOffset + ImGui.GetTextLineHeight());
                    ImGui.TableSetColumnIndex(1);


                    if (m_gitRepo.CommitNotes.TryGetValue(item.Sha, out List<string> notes))
                    {
                        if (notes != null && notes.Count > 0)
                        {
                            foreach (var itemNote in notes)
                            {
                                var noteRectMin = ImGui.GetWindowPos() - new Vector2(ImGui.GetScrollX(), ImGui.GetScrollY()) + ImGui.GetCursorPos();
                                var noteRectMax = noteRectMin + ImGui.CalcTextSize(itemNote);

                                //ImGuiView.Colors[1]-Vector4.One*0.5f)
                                ImGui.GetWindowDrawList().AddRectFilled(noteRectMin, noteRectMax, ImGui.GetColorU32(ImGuiCol.TextSelectedBg));

                                //int colorIndex = branchIndex % ImGuiView.Colors.Count;
                                var textColor = ImGuiView.Colors[0];

                                ImGui.Text(itemNote);
                                //ImGui.TextColored(textColor, itemNote);
                                ImGui.SameLine();
                            }
                        }
                    }

                    //ImGui.Text(item.MessageShort);
                    if (ImGui.Selectable(item.MessageShort, m_selectCommit != null && m_selectCommit.Sha == item.Sha, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        m_gitRepo.SelectCommit = item;
                    }

                    //右键菜单 - test
                    if (ImGui.BeginPopupContextItem(item.Sha))
                    {
                        m_gitRepo.SelectCommit = item;
                        if (m_gitRepo.SelectCommit != null)
                        {
                            ImGui.Text(Icon.Get(Icon.Material_commit));
                            ImGui.SameLine();
                            ImGui.Text(item.Sha.Substring(0, 10));
                            ImGui.SameLine();
                            ImGui.Text(item.MessageShort);
                            ImGui.Separator();

                            m_plugin.CallPopupContextItem("OnCommitPopupItem");
                            //var viewCommands = GitCommandView.ViewCommands.FindAll(x => x.Target == ViewCommandTarget.Commit);
                            //foreach (var itemViewCommand in viewCommands)
                            //{
                            //    if (ImGui.MenuItem(itemViewCommand.Name))
                            //    {
                            //        GitCommandView.RunGitCommandView<CommonProcessGitCommand>(m_gitRepo, itemViewCommand);
                            //    }
                            //}
                        }
                        ImGui.EndPopup();
                    }

                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(item.Author.When.DateTime.ToString());
                    ImGui.TableSetColumnIndex(3);
                    ImGui.Text(item.Author.Name);// [{item.Committer.Email}]
                    ImGui.TableSetColumnIndex(4);
                    ImGui.Text($"{item.Sha.Substring(0, 10)}");


                }

                Pool<CommitAtlasLine>.Release(commitAtlasLines);

                ImGui.EndTable();

            }

        }

       


        private void DrawSelectCommit()
        {
            m_selectCommitDiffSpliteView.Begin();

            m_selectCommitTreeSpliteView.Begin();
            //提交信息
            DrawSelectCommitInfo();
            m_selectCommitTreeSpliteView.Separate();
            //文件树
            DrawSelectCommitTree();
            m_selectCommitTreeSpliteView.End();

            m_selectCommitDiffSpliteView.Separate();
            //绘制选择文件
            DrawSelectCommitDiff();
            m_selectCommitDiffSpliteView.End();


        }


        private void DrawSelectCommitInfo()
        {
            if (m_selectCommit != m_gitRepo.SelectCommit)
            {
                BuildSelectCommitPatch(m_gitRepo.SelectCommit);
                m_selectCommit = m_gitRepo.SelectCommit;
                return;
            }

            if (m_selectCommit == null)
            {
                return;
            }

            ImGui.Text($"Sha: {m_selectCommit.Sha}");
            ImGui.Text("Parents:");
            if (m_selectCommit.Parents != null)
            {
                foreach (var itemParent in m_selectCommit.Parents)
                {
                    ImGui.SameLine();
                    if (ImGui.Button(itemParent.Sha.Substring(0, 10)))
                    {
                        m_gitRepo.SelectCommit = itemParent;
                        //SelectCommit(itemParent);
                    }
                }
            }
            ImGui.Text($"Author: {m_selectCommit.Author.Name} <{m_selectCommit.Author.Email}>");
            ImGui.Text($"DateTime: {m_selectCommit.Author.When.DateTime.ToString()}");
            //ImGui.Text($"Committer: {m_selectCommit.Author} {m_selectCommit.Email}\n");
            ImGui.Spacing();
            ImGui.Text(m_selectCommit.Message);
        }

        private void DrawSelectCommitTree()
        {
            if (m_selectCommitPatch != null)
            {
                foreach (var item in m_selectCommitPatch)
                {
                    if (ImGui.RadioButton(item.Path, m_selectCommitPatchEntry == item))
                    {
                        m_selectCommitPatchEntry = item;
                        m_showDiffText.BuildDiffTexts(item.Patch);
                    }
                }
            }
        }

        private void DrawSelectCommitDiff()
        {
            if (m_selectCommitPatchEntry != null)
            {
                m_showDiffText.Draw();
            }
        }


        private void BuildSelectCommitPatch(Commit gitRepoCommit)
        {
            m_selectCommit = gitRepoCommit;
            m_selectCommitPatch = null;
            m_selectCommitPatchEntry = null;

            //子线程取真正的数据绘制
            Task.Run(() =>
            {
                if (m_selectCommit != null)
                {
                    //CommitFilter commitFilter = new CommitFilter();
                    if (m_selectCommit != null)
                    {
                        if (m_selectCommit.Parents != null && m_selectCommit.Parents.Count() > 0)
                        {
                            foreach (var itemParent in m_selectCommit.Parents)
                            {
                                var diffPatch = m_gitRepo.Diff.Compare<Patch>(itemParent.Tree, m_selectCommit.Tree);
                                if (m_selectCommitPatch == null)
                                {
                                    m_selectCommitPatch = diffPatch;
                                }
                                else
                                {
                                    foreach (var item in diffPatch)
                                    {
                                        m_selectCommitPatch.Append(item);
                                    }
                                }
                            }
                        }

                    }
                }


            });
        }

        private float GetScrollInterval(float size)
        {
            return ImGui.GetScrollMaxY() * (size / m_commitViewMax);
        }

        IEnumerable<Commit> GetHistoryCommits()
        {
            var range = new Range(m_commitViewIndex, m_commitViewIndex + m_commitViewMax);
            if (!range.Equals(m_cacheRange))
            {
                m_cacheCommits = m_gitRepo.Repo.Commits.Take(range);
                m_cacheRange = range;
            }
            return m_cacheCommits;
        }



        public class CommitAtlasLine : IPool
        {
            public Commit Parent;
            public Vector2 ChildPoint;
            public int AtlasId;
            public void OnGet()
            {
            }

            public void OnRelease()
            {
                Parent = null;
                ChildPoint = Vector2.Zero;
                AtlasId = 0;
            }
        }
    }
}
