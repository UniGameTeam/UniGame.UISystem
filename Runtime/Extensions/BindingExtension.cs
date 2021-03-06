﻿using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UniModules.UniCore.Runtime.Utils;
using UniModules.UniGame.Rx.Runtime.Extensions;
using UniModules.UniGame.UISystem.Runtime.Abstract;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace UniModules.UniGame.UiSystem.Runtime.Extensions
{
    using Core.Runtime.DataFlow.Interfaces;
    using global::UniCore.Runtime.ProfilerTools;
    using UniCore.Runtime.Rx.Extensions;
    using UniModules.UniGame.Core.Runtime.Interfaces;

    public static class BindingExtension
    {
        
        #region ugui extensions

        public static TView Bind<TView,TValue>(this TView view, IObservable<TValue> source, Button command, int frameThrottle = 0)
            where TView : class,IView
        {
            Bind(source,x => command.onClick?.Invoke(),frameThrottle)
                .AddTo(view.ModelLifeTime);
            return view;
        }
        
        public static TView Bind<TView>(this TView view, Button source, Action<Unit> command, int frameThrottle = 0)
            where TView : class,IView
        {
            Bind(source.OnClickAsObservable(),command,frameThrottle)
                .AddTo(view.ModelLifeTime);
            return view;
        }
        
        public static TView Bind<TView>(this TView view, Button source, IReactiveCommand<Unit> command, int frameThrottle = 0)
            where TView : class,IView
        {
            Bind(source.OnClickAsObservable(),command,frameThrottle)
                .AddTo(view.ModelLifeTime);
            return view;
        }
        
        public static TView Bind<TView,TValue>(this TView view, IObservable<TValue> source, IReactiveCommand<TValue> command, int frameThrottle = 0)
            where TView : class,IView
        {
            Bind(source, command,frameThrottle).AddTo(view.ModelLifeTime);
            return view;
        }
        
        public static TView Bind<TView>(this TView view, IObservable<Sprite> source, Image image, int frameThrottle = 0)
            where TView : class,IView
        {
            return view.Bind(source, view.ModelLifeTime, x => image.sprite = x,frameThrottle);
        }
        
        public static TView Bind<TView>(this TView view, IObservable<Sprite> source, Button button, int frameThrottle = 0)
            where TView : class,IView
        {
            return view.Bind(source, view.ModelLifeTime, x => button.image.sprite = x,frameThrottle);
        }
        
        public static TView Bind<TView>(this TView view, IObservable<string> source, TextMeshProUGUI text, int frameThrottle = 0)
            where TView : class,IView
        {
            return view.Bind(source, view.ModelLifeTime, x => text.text = x,frameThrottle);
        }

        public static TView Bind<TView>(this TView view, IObservable<string> source, TextMeshPro text, int frameThrottle = 0)
            where TView : class,IView
        {
            return view.Bind(source, view.ModelLifeTime, x => text.text = x,frameThrottle);
        }
        
        public static TView Bind<TView>(this TView view, IObservable<int> source, TextMeshPro text, int frameThrottle = 0)
            where TView : class,IView
        {
            return view.Bind(source, view.ModelLifeTime, x => text.text = x.ToStringFromCache(),frameThrottle);
        }
        
        public static TView Bind<TView>(this TView view, IObservable<int> source, TextMeshProUGUI text, int frameThrottle = 0)
            where TView : class,IView
        {
            return view.Bind(source, view.ModelLifeTime, x => text.text = x.ToStringFromCache(),frameThrottle);
        }
        
        #endregion


        public static async UniTask<TView> Bind<TView,TModel>(this TView view,TModel model, IView target) 
            where TView : class, IView
            where TModel : IViewModel
        {
            await target.Initialize(model);
            return view;
        }
        
        public static IDisposable Bind<T>(
            this IObservable<T> source, 
            Action<T> target, 
            int frameThrottle = 1)
        {
            if (source != null)
                return frameThrottle < 1
                    ? source.Subscribe(target)
                    : source.BatchPlayerTiming(frameThrottle, PlayerLoopTiming.LastPostLateUpdate).Subscribe(target);
            
            GameLog.LogWarning($"BindingExtension: NULL IObservable<T> detected with type {typeof(T).Name}");
            return Disposable.Empty;

        }
        public static IDisposable Bind<T>(
            this IObservable<T> source, 
            IReactiveCommand<T> target, 
            int frameThrottle = 1)
        {
            if(source == null)
                return Disposable.Empty;
            
            if (frameThrottle < 1)
            {
                return source.Where(x => target.CanExecute.Value).
                    Subscribe(x => target.Execute(x));
            }
            return source.
                BatchPlayerTiming(frameThrottle,PlayerLoopTiming.LastPostLateUpdate).
                Where(x => target.CanExecute.Value).
                Subscribe(x => target.Execute(x));
        }
        
        public static TSource Bind<TSource,T>(
            this TSource view,
            IObservable<T> source, 
            Action<T> target, 
            int frameThrottle = 1)
            where TSource : IView
        {
            source.Bind(target,frameThrottle).AddTo(view.ModelLifeTime);
            return view;
        }
        
        public static IViewModel Bind<T>(
            this IViewModel model,
            IObservable<T> source, 
            Action<T> target, 
            int frameThrottle = 1)
        {
            source.Bind(target,frameThrottle).AddTo(model.LifeTime);
            return model;
        }

        public static TSource Bind<TSource, T>(this TSource view, IObservable<T> source, ILifeTime lifeTime, Action<T> target, int frameThrottle = 1) 
            where TSource : ILifeTimeContext
        {
            source.Bind(target, frameThrottle).AddTo(lifeTime);
            return view;
        }

    }
}
