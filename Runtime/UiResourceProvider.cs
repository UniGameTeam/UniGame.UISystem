﻿using UniModules.UniGame.ViewSystem.Runtime.Extensions;
using UnityEngine;

namespace UniGame.UiSystem.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Addressables.Reactive;
    using Settings;
    using UniModules.UniCore.Runtime.ObjectPool.Runtime;
    using UniModules.UniCore.Runtime.ObjectPool.Runtime.Extensions;
    using UniModules.UniGame.UISystem.Runtime.Abstract;
    using Object = UnityEngine.Object;

    public class UiResourceProvider : IViewResourceProvider<Component>
    {
        private Dictionary<Type, List<UiViewReference>> views = new Dictionary<Type, List<UiViewReference>>(32);

        public IAddressableObservable<Component> LoadViewAsync(Type viewType,
            string skinTag = "",
            bool strongMatching = true,
            string viewName = "")
        {
            var items = FindItemsByType(viewType, strongMatching);

            var item = items.SelectReference(skinTag,viewName);

            //return collection to pool
            items.Despawn();

            if (item == null)
            {
                Debug.LogError($"{nameof(UiResourceProvider)} ITEM MISSING skin:{skinTag} type {viewType.Name}");
                return null;
            }

            return item.View.ToObservable<Component>();
        }

        public List<IAddressableObservable<Component>> LoadViewsAsync(Type viewType, string skinTag = null, bool strongMatching = true)
        {
            var items = FindItemsByType(viewType, strongMatching);
            //return collection to pool
            items.Despawn();

            if (items.Count <= 0)
            {
                Debug.LogError($"{nameof(UiResourceProvider)} ITEM MISSING skin:{skinTag} type {viewType.Name}");
                return null;
            }

            var result = new List<IAddressableObservable<Component>>();

            foreach (var item in items)
            {
                result.Add(item.View.ToObservable<Component>());
            }

            return result;
        }

        public void RegisterViews(IReadOnlyList<UiViewReference> sourceViews)
        {
            foreach (var view in sourceViews)
            {
                Type targetType = view.Type;
                if (views.TryGetValue(targetType, out var items) == false)
                {
                    items = new List<UiViewReference>();
                    views[targetType] = items;
                }
                items.Add(view);
            }
        }

        private List<UiViewReference> FindItemsByType(Type type, bool strongMatching)
        {
            var result = ClassPool.Spawn<List<UiViewReference>>();
            if (strongMatching)
            {
                if (views.TryGetValue(type, out var items))
                {
                    result.AddRange(items);
                }
                return result;
            }

            foreach (var view in views)
            {
                var viewType = view.Key;
                if (type.IsAssignableFrom(viewType))
                    result.AddRange(view.Value);
            }

            return result;
        }

    }
}
