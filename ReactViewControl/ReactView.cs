﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WebViewControl;

namespace ReactViewControl {

    public delegate void ResourceRequestedEventHandler(ResourceHandler resourceHandler);

    public delegate Stream CustomResourceRequestedEventHandler(string resourceKey, params string[] options);

    public abstract class ReactView : UserControl, IDisposable {

        private static Dictionary<Type, ReactViewRender> CachedViews { get; } = new Dictionary<Type, ReactViewRender>();

        private ReactViewRender View { get; }

        private static ReactViewRender CreateReactViewInstance(ReactViewFactory factory) {
            ReactViewRender InnerCreateView() {
                var view = new ReactViewRender(factory.DefaultStyleSheet, () => factory.InitializePlugins(), factory.EnableViewPreload, factory.EnableDebugMode, factory.DevServerURI);
                if (factory.ShowDeveloperTools) {
                    view.ShowDeveloperTools();
                }

                return view;
            }

            if (factory.EnableViewPreload) {
                var factoryType = factory.GetType();
                // check if we have a view cached for the current factory
                if (CachedViews.TryGetValue(factoryType, out var cachedView)) {
                    CachedViews.Remove(factoryType);
                }

                // create a new view in the background and put it in the cache
                Dispatcher.CurrentDispatcher.BeginInvoke((Action)(() => {
                    if (!CachedViews.ContainsKey(factoryType) && !Dispatcher.CurrentDispatcher.HasShutdownStarted) {
                        CachedViews.Add(factoryType, InnerCreateView());
                    }
                }), DispatcherPriority.Background);

                if (cachedView != null) {
                    return cachedView;
                }
            }

            return InnerCreateView();
        }

        protected ReactView(IViewModule mainModule) {
            View = CreateReactViewInstance(Factory);
            SetResourceReference(StyleProperty, typeof(ReactView)); // force styles to be inherited, must be called after view is created otherwise view might be null

            View.Host = this;
            MainModule = mainModule;
            // bind main module (this is needed so that the plugins are available right away)
            View.BindComponent(mainModule);

            IsVisibleChanged += OnIsVisibleChanged;

            Content = View;

            FocusManager.SetIsFocusScope(this, true);
            FocusManager.SetFocusedElement(this, View.FocusableElement);
        }

        ~ReactView() {
            Dispose();
        }

        public void Dispose() {
            InnerDispose();
            View.Dispose();
            GC.SuppressFinalize(this);
        }

        protected virtual void InnerDispose() { }

        /// <summary>
        /// Factory used to configure the initial properties of the control.
        /// </summary>
        protected virtual ReactViewFactory Factory => new ReactViewFactory();

        protected void RefreshDefaultStyleSheet() {
            View.DefaultStyleSheet = Factory.DefaultStyleSheet;
            Dispatcher.CurrentDispatcher.BeginInvoke((Action)(() => {
                CachedViews.Remove(Factory.GetType()); 
            }), DispatcherPriority.Background);
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
            base.OnPropertyChanged(e);

            // IWindowService is a WPF internal property set when component is loaded into a new window, even if the window isn't shown
            if (e.Property.Name == "IWindowService") {
                if (e.OldValue is Window oldWindow) {
                    oldWindow.IsVisibleChanged -= OnWindowIsVisibleChanged;
                }

                if (e.NewValue is Window newWindow) {
                    newWindow.IsVisibleChanged += OnWindowIsVisibleChanged;
                }
            }
        }

        private void OnWindowIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            var window = (Window)sender;
            // this is the first event that we have available with guarantees that all the component properties have been set
            // since its not supposed to change properties once the component has been shown
            if (window.IsVisible) {
                window.IsVisibleChanged -= OnWindowIsVisibleChanged;
                TryLoadComponent();
            }
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            // fallback when window was already shown
            if (IsVisible) {
                IsVisibleChanged -= OnIsVisibleChanged;
                TryLoadComponent();
            }
        }

        private void TryLoadComponent() {
            if (!View.IsMainComponentLoaded) {
                View.LoadComponent(MainModule);
            }
        }

        /// <summary>
        /// Retrieves the specified plugin module instance.
        /// </summary>
        /// <typeparam name="T">Type of the plugin to retrieve.</typeparam>
        /// <returns></returns>
        public T WithPlugin<T>() {
            return View.WithPlugin<T>();
        }

        /// <summary>
        /// Enables or disables debug mode. 
        /// In debug mode the webview developer tools becomes available pressing F12 and the webview shows an error message at the top with the error details 
        /// when a resource fails to load.
        /// </summary>
        public bool EnableDebugMode { get => View.EnableDebugMode; set => View.EnableDebugMode = value; }

        /// <summary>
        /// True when the main component has been rendered.
        /// </summary>
        public bool IsReady => View.IsReady;

        /// <summary>
        /// Gets or sets the control zoom percentage (1 = 100%)
        /// </summary>
        public double ZoomPercentage { get => View.ZoomPercentage; set => View.ZoomPercentage = value; }

        /// <summary>
        /// Event fired when the component is rendered and ready for interaction.
        /// </summary>
        public event Action Ready {
            add { View.Ready += value; }
            remove { View.Ready -= value; }
        }

        /// <summary>
        /// Event fired when an async exception occurs (eg: executing javascript)
        /// </summary>
        public event UnhandledAsyncExceptionEventHandler UnhandledAsyncException {
            add { View.UnhandledAsyncException += value; }
            remove { View.UnhandledAsyncException -= value; }
        }

        /// <summary>
        /// Event fired when a resource fails to load.
        /// </summary>
        public event ResourceLoadFailedEventHandler ResourceLoadFailed {
            add { View.ResourceLoadFailed += value; }
            remove { View.ResourceLoadFailed -= value; }
        }

        /// <summary>
        /// Handle embedded resource requests. You can use this event to change the resource being loaded.
        /// </summary>
        public event ResourceRequestedEventHandler EmbeddedResourceRequested {
            add { View.EmbeddedResourceRequested += value; }
            remove { View.EmbeddedResourceRequested -= value; }
        }

        /// <summary>
        /// Handle custom resource requests. Use this event to load the resource based on the provided key.
        /// This handler will be called before the frame handler.
        /// </summary>
        public event CustomResourceRequestedEventHandler CustomResourceRequested {
            add { View.CustomResourceRequested += value; }
            remove { View.CustomResourceRequested -= value; }
        }

        /// <summary>
        /// Handle external resource requests. 
        /// Call <see cref="WebView.ResourceHandler.BeginAsyncResponse"/> to handle the request in an async way.
        /// </summary>
        public event ResourceRequestedEventHandler ExternalResourceRequested {
            add { View.ExternalResourceRequested += value; }
            remove { View.ExternalResourceRequested -= value; }
        }

        /// <summary>
        /// Handle drag of files. Use this event to get the full path of the files being dragged.
        /// </summary>
        public event FilesDraggingEventHandler FilesDragging {
            add { View.FilesDragging += value; }
            remove { View.FilesDragging -= value; }
        }

        /// <summary>
        /// Handle drag of text. Use this event to get the text content being dragged.
        /// </summary>
        public event TextDraggingEventHandler TextDragging {
            add { View.TextDragging += value; }
            remove { View.TextDragging -= value; }
        }

        /// <summary>
        /// Opens the developer tools.
        /// </summary>
        public void ShowDeveloperTools() {
            View.ShowDeveloperTools();
        }

        /// <summary>
        /// Closes the developer tools.
        /// </summary>
        public void CloseDeveloperTools() {
            View.CloseDeveloperTools();
        }

        /// <summary>
        /// View module of this control.
        /// </summary>
        protected IViewModule MainModule { get; }

        /// <summary>
        /// Number of preloaded views that are mantained in cache for each view.
        /// Components with different property values are stored in different cache entries.
        /// Defaults to 6. 
        /// </summary>
        public static int PreloadedCacheEntriesSize { get; set; } = 6;

        /// <summary>
        /// Called when executing a native method.
        /// </summary>
        protected virtual object OnNativeMethodCalled(Func<object> nativeMethod) {
            return nativeMethod();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal object CallNativeMethod(Func<object> nativeMethod) {
            return OnNativeMethodCalled(nativeMethod);
        }
    }
}