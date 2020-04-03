﻿using System.Reflection;
using System.Runtime.Loader;

namespace WebViewControl {

    internal class AssemblyLoader {

        internal static Assembly LoadAssembly(string path) => AssemblyLoadContext.Default.LoadFromAssemblyPath(path);

    }
}