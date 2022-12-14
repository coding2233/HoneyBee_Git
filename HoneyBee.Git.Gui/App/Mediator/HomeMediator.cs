using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.App.Model;
using Wanderer.App.Service;
using Wanderer.App.View;

namespace Wanderer.App.Mediator
{
    internal class HomeMediator: EventMediator
    {
        [Inject]
        public HomeView homeView { get; set; }

        //[Inject]
        //public IAppModel appModel { get; set; }

        [Inject]
        public IDatabaseService database { get; set; }


        public override void OnRegister()
        {
            base.OnRegister();
            homeView.OnOpenRepository += OnOpenRepositoryCallback;
            homeView.OnRemoveRepository += OnRemoveRepositoryCallback;
        }

        public override void OnRemove()
        {
            homeView.OnOpenRepository -= OnOpenRepositoryCallback;
            homeView.OnRemoveRepository -= OnRemoveRepositoryCallback;

            base.OnRemove();
        }

        public override void OnEnable()
        {
            base.OnEnable();

            homeView.SetRepositories(database.GetRepositories());
        }

        private void OnOpenRepositoryCallback(string gitPath)
        {
            dispatcher.Dispatch(AppEvent.ShowGitRepo, gitPath);
        }

        private void OnRemoveRepositoryCallback(string gitPath)
        {
            database.RemoveRepository(gitPath);
            homeView.SetRepositories(database.GetRepositories());
        }

    }
}
