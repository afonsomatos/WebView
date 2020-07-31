﻿using Avalonia;
using Avalonia.Markup.Xaml;

namespace Tests {

    public class App : Application {

        public static new App Current => (App)Application.Current;

        public App() { }

        public override void Initialize() {
            WebViewControl.WebView.OsrEnabled = false;
            AvaloniaXamlLoader.Load(this);
        }
    }
}