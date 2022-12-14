using strange.extensions.command.impl;
using strange.extensions.context.api;
using strange.extensions.context.impl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Wanderer.App.Service;
using Wanderer.App.View;
using Wanderer.Common;
using Wanderer.GitRepository;
using Wanderer.GitRepository.View;

namespace Wanderer.App.Controller
{
    public class StartCommand:EventCommand
    {
        [Inject(ContextKeys.CONTEXT_VIEW)]
        public MonoBehaviour contextView { get; set; }

        [Inject(ContextKeys.CONTEXT)]
        public IContext context { get; set; }
        
        [Inject]
        public IPluginService pluginService { get; set; }

        public override void Execute()
        {
            //主窗口
            ImGuiView.Create<AppImGuiView>(context,0);
            
            //内容主窗口
            ImGuiView.Create<HomeView>(context,0);

            //运行插件
            pluginService.Reload();
        }
    }
}
