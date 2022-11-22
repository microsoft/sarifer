// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Microsoft.CodeAnalysis.Sarif.Sarifer
{
    internal class AppDomainWrapper : IDisposable
    {
        private const string DomainName = "SpamPluginSubDomain";

        private static readonly string assemblyFullPath = Assembly.GetExecutingAssembly().Location;

        private static readonly string assemblyFolder = Path.GetDirectoryName(assemblyFullPath);

        private AppDomain childDomain;

        internal AppDomainWrapper()
        {
            string subDomainName = DomainName + Guid.NewGuid().ToString();

            AppDomainSetup domainSetup = new AppDomainSetup
            {
                PrivateBinPath = assemblyFolder,
                ApplicationBase = Environment.CurrentDirectory,
                ApplicationName = subDomainName,
            };

            this.childDomain = AppDomain.CreateDomain(subDomainName, null, domainSetup);
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        public AppDomain ChildDomain => this.childDomain;

        public T CreateInstance<T>()
        {
            object instance = this.childDomain.CreateInstanceFromAndUnwrap(assemblyFullPath, typeof(T).FullName);

            if (instance is T t)
            {
                return t;
            }

            try
            {
                return (T)Convert.ChangeType(instance, typeof(T));
            }
            catch (InvalidCastException ice)
            {
                Trace.WriteLine($"Cannot create the instance of {typeof(T).Name}. Exception: {ice}");
                return default;
            }
        }

        public void Dispose()
        {
            if (this.childDomain != null)
            {
                AppDomain.Unload(this.childDomain);
                this.childDomain = null;
            }

            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                Assembly assembly = Assembly.Load(args.Name);
                if (assembly != null)
                {
                    return assembly;
                }
            }
            catch
            {
                // ignore load error
            }

            // If not able to load assembly above, try to load by filename in the same dir of original assembly
            // example args.Name: Microsoft.Sarif.Sarifer, Version=3.0.0.0, Culture=neutral, PublicKeyToken=null
            string[] parts = args.Name.Split(',');
            string file = Path.GetDirectoryName(assemblyFullPath) + "\\" + parts[0].Trim() + ".dll";

            try
            {
                return Assembly.LoadFrom(file);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Cannot resolve assembly {file}. Exception: {ex}");
            }

            return null;
        }
    }
}
