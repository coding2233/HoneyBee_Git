﻿using strange.extensions.context.impl;
using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.Common
{
    public abstract class ImGuiView : View
    {
        private static List<ImGuiView> s_imGuiViews=new List<ImGuiView>();

        protected int m_priority;
        public int priority
        {
            get
            {
                return m_priority;
            }
            set
            {
                m_priority = value;
                //排序
                s_imGuiViews.Sort((x, y) => { 
                    return x.priority - y.priority;
                });
            }
        }

        public ImGuiView(ContextView contextView) : base(contextView)
        {
        }

        public virtual void OnDraw()
        { }

        public static void Render()
        {
            for (int i = 0; i < s_imGuiViews.Count; i++)
            {
                s_imGuiViews[i].OnDraw();
            }
        }


        public static T Create<T>(ContextView contextView,int defaultPriority=0) where T: ImGuiView
        {
            object[] args = null;
            if (contextView != null)
            {
                args = new object[] { contextView };
            }
            T view = Activator.CreateInstance(typeof(T), args) as T;
            if (defaultPriority != 0)
            {
                view.priority = defaultPriority;
            }
            return view;
        }

        public static void Destory(ImGuiView view)
        {
            if (view != null)
            {
                if (s_imGuiViews.Contains(view))
                {
                    s_imGuiViews.Remove(view);
                }
                view.OnDispose();
                view = null;
            }
        }

        public static void DestoryAll()
        {
            while (s_imGuiViews.Count > 0)
            {
                var view = s_imGuiViews[0];
                s_imGuiViews.RemoveAt(0);
                view?.Dispose();
                view = null;
            }
        }


    }
}
