﻿namespace UniGame.UiSystem.Runtime
{
    using System;
    using System.Collections;
    using Cysharp.Threading.Tasks;
    using JetBrains.Annotations;
    using UniCore.Runtime.ProfilerTools;
    using UniModules.UniCore.Runtime.Attributes;
    using UniModules.UniCore.Runtime.DataFlow;
    using UniModules.UniCore.Runtime.Rx.Extensions;
    using UniModules.UniGame.Core.Runtime.Rx;
    using UniModules.UniGame.UiSystem.Runtime.Extensions;
    using UniModules.UniRoutine.Runtime;
    using UniModules.UniGame.Core.Runtime.DataFlow.Interfaces;
    using UniModules.UniGame.UISystem.Runtime;
    using UniModules.UniGame.UISystem.Runtime.Abstract;
    using UniRx;
    using UniModules.UniRoutine.Runtime.Extension;
    using UnityEngine;
    using UnityEngine.EventSystems;
    
    public abstract class ViewBase : 
        UIBehaviour, 
        ILayoutView
    {
        private const string NullViewName = "Null";
        
        #region inspector

        [ReadOnlyValue]
        [SerializeField]
        [UsedImplicitly]
        private bool _isVisible;

        [ReadOnlyValue]
        [SerializeField]
        private BoolRecycleReactiveProperty _isInitialized = new BoolRecycleReactiveProperty();

        [ReadOnlyValue]
        [SerializeField]
        private ViewStatus _editorViewStatus = ViewStatus.None;

        [HideInInspector]
        [SerializeField]
        public string skinTag = string.Empty;
        
        #endregion

        private ViewStatus _internalViewStatus = ViewStatus.None;
        
        private RectTransform _rectTransform;
        private Transform _transform;
        
        private readonly LifeTimeDefinition _lifeTimeDefinition = new LifeTimeDefinition();
        private readonly LifeTimeDefinition _progressLifeTime   = new LifeTimeDefinition();
        private readonly LifeTimeDefinition _viewModelLifeTime   = new LifeTimeDefinition();

        /// <summary>
        /// ui element visibility status
        /// </summary>
        private readonly BoolRecycleReactiveProperty _visibility = new BoolRecycleReactiveProperty();

        /// <summary>
        /// view statuses reactions
        /// </summary>
        private readonly RecycleReactiveProperty<ViewStatus> _status = new RecycleReactiveProperty<ViewStatus>();

        private IViewLayoutProvider _viewLayout;

        private bool _isViewOwner;
        
        #region public properties

        public IReadOnlyReactiveProperty<bool> IsInitialized => _isInitialized;
        
        public GameObject Owner => gameObject;

        /// <summary>
        /// View LifeTime
        /// </summary>
        public ILifeTime LifeTime => _lifeTimeDefinition;

        public ILifeTime ModelLifeTime => _viewModelLifeTime;
        
        /// <summary>
        /// view transform
        /// </summary>
        public RectTransform RectTransform => _rectTransform != null ? 
            _rectTransform:
            _rectTransform = transform as RectTransform;

        
        /// <summary>
        /// view transform
        /// </summary>
        public Transform Transform => _transform = _transform ? _transform : transform;
        
        /// <summary>
        /// views layout
        /// </summary>
        public IViewLayoutProvider Layout => _viewLayout;

        public IReadOnlyReactiveProperty<ViewStatus> Status => _status;

        /// <summary>
        /// Is View Active
        /// </summary>
        public IReadOnlyReactiveProperty<bool> IsVisible => _visibility;

        public bool IsTerminated => _lifeTimeDefinition.IsTerminated;

        public IViewModel ViewModel { get; private set; }

        #endregion public properties

        #region public methods

        /// <summary>
        /// complete view lifetime immediately
        /// </summary>
        public void Destroy() => _lifeTimeDefinition.Release();

        public void BindLayout(IViewLayoutProvider layoutProvider)
        {
            _viewLayout = layoutProvider;
        }

        public IView BindNested(ILayoutView view, IViewModel model)
        {
            view?.BindLayout(_viewLayout);
            view?.Initialize(model);
            return this;
        }

        public async UniTask<IView> Initialize(IViewModel model, IViewLayoutProvider layoutProvider)
        {
            _isInitialized.Value = false;
            
            BindLayout(layoutProvider);
            await Initialize(model);

            _isInitialized.Value = true;

            return this;
        }

        public async UniTask<IView> Initialize(IViewModel model, bool isViewOwner = false)
        {
            // save current state
            _isViewOwner = isViewOwner;
            
            //restart view lifetime
            _viewModelLifeTime.Release();
            _progressLifeTime.Release();

            //calls one per lifetime
            if (!_isInitialized.Value) {
                InitialSetup();
                OnAwake();
            }

            InitializeHandlers(model);
            
            BindLifeTimeActions(model);

            //custom initialization
            await OnInitialize(model);

            return this;
        }

        /// <summary>
        /// show active view
        /// </summary>
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.Button]
#endif
        public void Show()
        {
            if(!SetInternalStatus(ViewStatus.Shown))
                return;
            
            StartProgressAction(_progressLifeTime, OnShow);
        }

        /// <summary>
        /// hide view without release it
        /// </summary>
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.Button]
#endif
        public void Hide()
        {
            if(!SetInternalStatus(ViewStatus.Hidden))
                return;
            
            StartProgressAction(_progressLifeTime, OnHide);
        }

        /// <summary>
        /// end of view lifetime
        /// </summary>
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.Button]
#endif
        public void Close()
        {
            if(!SetInternalStatus(ViewStatus.Closed))
                return;
            
            StartProgressAction(_progressLifeTime, OnClose);
        }

        
        /// <summary>
        /// bind source stream to view action
        /// with View LifeTime context
        /// </summary>
        public IView BindTo<T>(IObservable<T> source, Action<T> action)
        {
            return BindToView(source, action);
        }
        
        /// <summary>
        /// bind source stream to view action
        /// with View LifeTime context
        /// </summary>
        public IView BindThrottleTo<T>(IObservable<T> source, Action<T> action,int frameThrottle = 1)
        {
            return BindToView(source, action,frameThrottle);
        }

        /// <summary>
        /// bind source stream to view action
        /// with View LifeTime context
        /// </summary>
        public IView BindToView<T>(IObservable<T> source, Action<T> action, int frameThrottle = 0)
        {
            return this.Bind(source, _viewModelLifeTime, action,frameThrottle);
        }
        
        public IObservable<IView> SelectStatus(ViewStatus status)
        {
            return _status.
                Where(x => x == status).
                Select(x => this);
        }

        public override bool IsActive()
        {
            return base.IsActive() && (_status.Value == ViewStatus.Showing || _status.Value == ViewStatus.Shown);
        }

        #endregion public methods

        #region private methods

        /// <summary>
        /// custom initialization methods
        /// </summary>
        protected virtual UniTask OnInitialize(IViewModel model)
        {
            return UniTask.CompletedTask;
        }
        
        /// <summary>
        /// view closing process
        /// windows auto terminated on close complete
        /// </summary>
        /// <returns></returns>
        private IEnumerator OnClose()
        {
            try
            {
                //wait until user defined closing operation complete
                yield return OnCloseProgress(_progressLifeTime);
            }
            finally
            {
                _lifeTimeDefinition.Release();
            }
        }
        
        /// <summary>
        /// hide process
        /// </summary>
        private IEnumerator OnHide()
        {
            if(!SetStatus(ViewStatus.Hiding))
                yield break;
            
            //wait until user defined closing operation complete
            yield return OnHidingProgress(_progressLifeTime);

            SetStatus(ViewStatus.Hidden);
        }
        
        /// <summary>
        /// show process
        /// </summary>
        private IEnumerator OnShow()
        {
            yield return this.WaitForEndOfFrame();

            if (!SetStatus(ViewStatus.Showing))
                yield break;

            yield return OnShowProgress(_progressLifeTime);

            SetStatus(ViewStatus.Shown);
        }

        protected virtual bool SetStatus(ViewStatus status)
        {
            if (_lifeTimeDefinition.IsTerminated)
            {
                _status.Value     = ViewStatus.Closed;
                _visibility.Value = false;
                return false;
            }

            switch (status)
            {
                case ViewStatus.Hidden:
                    _visibility.Value = false;
                    break;
                case ViewStatus.Showing:
                    _visibility.Value = true;
                    break;
            }

            _status.Value = status;

            return true;
        }

        /// <summary>
        /// close continuation
        /// use hiding progress by default
        /// </summary>
        protected virtual IEnumerator OnCloseProgress(ILifeTime progressLifeTime)
        {
            yield return OnHidingProgress(progressLifeTime);
        }

        /// <summary>
        /// showing continuation
        /// </summary>
        protected virtual IEnumerator OnShowProgress(ILifeTime progressLifeTime)
        {
            yield break;
        }
        
        /// <summary>
        /// hiding continuation
        /// </summary>
        protected virtual IEnumerator OnHidingProgress(ILifeTime progressLifeTime)
        {
            yield break;
        }

        private bool SetInternalStatus(ViewStatus internalStatus)
        {
            var viewName = this != null ? name : NullViewName;

#if UNITY_EDITOR || UNITY_DEBUG
            if (!this)
            {
                GameLog.LogWarning($"You try to {internalStatus} {viewName} but it has destroy status yet");
                return false;
            }
#endif

            if(_internalViewStatus == internalStatus)
            {
                GameLog.LogWarning($"You try to {internalStatus} {viewName} but it has {internalStatus} status yet");
                return false;
            }

            _internalViewStatus = internalStatus;

            return true;
        }

        private void StartProgressAction(LifeTimeDefinition lifeTime,Func<IEnumerator> action)
        {
            if (lifeTime.IsTerminated) 
                return;
            
            lifeTime.Release();
            
            //run animation immediately
            action().Execute(RoutineType.Update,true).AddTo(lifeTime);
        }

        private void InitializeHandlers(IViewModel model)
        {
            ViewModel = model;

            _isVisible = _visibility.Value;
            
            _visibility.
                Subscribe(x => _isVisible = x).
                AddTo(_lifeTimeDefinition);
            
        }
        
        private void BindLifeTimeActions(IViewModel model)
        {
            //bind model lifetime to local
            var modelLifeTime = model.LifeTime;
            if (model.IsDisposeWithModel)
            {
                modelLifeTime.ComposeCleanUp(_viewModelLifeTime, () =>
                {
                    if (Equals(ViewModel, model))
                        Close();
                });
            }

            _viewModelLifeTime.AddCleanUpAction(() =>
            {
                if (_isViewOwner)
                    ViewModel.Cancel();
            });
            
            _viewModelLifeTime.AddCleanUpAction(_progressLifeTime.Release);


#if UNITY_EDITOR
            _status.Subscribe(x => this._editorViewStatus = x).AddTo(LifeTime);
#endif
        }

        private void InitialSetup()
        {
            _isInitialized.Value = true;
            _status.Value  = ViewStatus.None;
            _internalViewStatus = ViewStatus.None;
            
            _viewModelLifeTime.AddTo(LifeTime);
            _progressLifeTime.AddTo(LifeTime);
            
            LifeTime.AddCleanUpAction(OnViewDestroy);
        }

        private void OnViewDestroy()
        {
            _viewLayout = null;
            _isInitialized.Value = false;
            ViewModel      = null;
            SetStatus(ViewStatus.Closed);
            _internalViewStatus = ViewStatus.Closed;
            _status.Release();
            _visibility.Release();
        }

        protected override void OnDisable()
        {
            _progressLifeTime.Release();
            base.OnDisable();
        }

        protected sealed override void OnDestroy()
        {
            _lifeTimeDefinition.Terminate();
            
            base.OnDestroy();
            GameLog.LogFormat("View {0} Destroyed",name);
        }

        protected virtual void OnAwake()
        {
        }

        #endregion
    }
}