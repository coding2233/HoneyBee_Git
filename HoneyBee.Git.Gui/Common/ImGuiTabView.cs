﻿using strange.extensions.context.api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.Common
{
    public class ImGuiTabView : ImGuiView
    {
        public bool Unsave { get; protected set; }

        public ImGuiTabView(IContext context) : base(context)
        {
        }


    }
}