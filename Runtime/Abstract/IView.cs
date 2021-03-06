﻿using UniModules.UniGame.Core.Runtime.DataFlow.Interfaces;
using UnityEngine;

namespace UniModules.UniGame.UISystem.Runtime.Abstract
{
    using System;
    using Core.Runtime.Interfaces;
    using Cysharp.Threading.Tasks;
    using UniRx;


    public interface IView : 
        ILifeTimeContext, 
        IViewStatus, 
        IViewCommands
    {
        
        public Transform Transform { get; }
        
        ILifeTime ModelLifeTime { get; }
        
        IReadOnlyReactiveProperty<bool> IsVisible { get; }

        IReadOnlyReactiveProperty<bool> IsInitialized { get; }

        IObservable<IView> SelectStatus(ViewStatus status);

        IView BindToView<T>(IObservable<T> source, Action<T> action, int frameThrottle = 0);
        
        /// <summary>
        /// is view lifetime finished
        /// </summary>
        bool IsTerminated { get; }

        UniTask<IView> Initialize(IViewModel vm, bool isViewOwner = false);

    }
}