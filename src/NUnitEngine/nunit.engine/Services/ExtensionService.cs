﻿// ***********************************************************************
// Copyright (c) 2015 Charlie Poole
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using Mono.Cecil;
using NUnit.Engine.Extensibility;
using NUnit.Engine.Internal;

namespace NUnit.Engine.Services
{
    public class ExtensionService : Service
    {
        private List<ExtensionPoint> _extensionPoints = new List<ExtensionPoint>();
        private Dictionary<string, ExtensionPoint> _pathIndex = new Dictionary<string, ExtensionPoint>();
        private Dictionary<Type, ExtensionPoint> _typeIndex = new Dictionary<Type, ExtensionPoint>();

        private List<ExtensionNode> _extensions = new List<ExtensionNode>();

        #region Public Properties

        public IEnumerable<ExtensionPoint> ExtensionPoints
        {
            get { return _extensionPoints; }
        }

        public IEnumerable<ExtensionNode> Extensions
        {
            get { return _extensions;  }
        }

        #endregion

        public ExtensionPoint GetExtensionPoint(string path)
        {
            return _pathIndex[path];
        }

        public ExtensionPoint GetExtensionPoint(Type type)
        {
            return _typeIndex[type];
        }

        public T[] GetExtensions<T>()
        {
            var extensions = new List<T>();

            var ep = GetExtensionPoint(typeof(T));
            foreach (var node in ep.Extensions)
                extensions.Add((T)node.ExtensionObject);

            return extensions.ToArray();
        }

        #region Service Overrides

        public override void StartService()
        {
            var thisAssembly = Assembly.GetExecutingAssembly();
            var startDir = new DirectoryInfo(AssemblyHelper.GetDirectoryName(thisAssembly));

            FindExtensionPoints(thisAssembly);
            FindExtensionsInDirectory(startDir);

            base.StartService();
        }

        private void FindExtensionPoints(Assembly assembly)
        {
            foreach (ExtensionPointAttribute attr in assembly.GetCustomAttributes(typeof(ExtensionPointAttribute), false))
            {
                var ep = new ExtensionPoint(attr.Path, attr.Type)
                {
                    Description = attr.Description
                };

                _extensionPoints.Add(ep);

                // TODO: These Adds will throw if the path or type is a duplicate.
                // We should give an error message instead.
                _pathIndex.Add(ep.Path, ep);
                _typeIndex.Add(ep.Type, ep);
            }
        }

        private void FindExtensionsInDirectory(DirectoryInfo startDir)
        {
            var addinsFiles = startDir.GetFiles("*.addins");
            if (addinsFiles.Length > 0)
                foreach (var file in addinsFiles)
                    ProcessAddinsFile(startDir.FullName, file.FullName);
            else
                foreach (var file in startDir.GetFiles("*.dll"))
                    FindExtensionsInAssembly(file.FullName);
        }

        private void ProcessAddinsFile(string baseDir, string fileName)
        {
            var doc = new XmlDocument();

            using (var rdr = new StreamReader(fileName))
            {
                doc.Load(rdr);
                foreach (XmlNode dirNode in doc.SelectNodes("Addins/Directory"))
                {
                    var path = Path.Combine(baseDir, dirNode.InnerText);
                    FindExtensionsInDirectory(new DirectoryInfo(path));
                }
            }
        }

        private void FindExtensionsInAssembly(string assemblyName)
        {
            var module = AssemblyDefinition.ReadAssembly(assemblyName).MainModule;
            foreach (var type in module.GetTypes())
                foreach (var attr in type.CustomAttributes)
                    if (attr.AttributeType.FullName == "NUnit.Engine.Extensibility.ExtensionAttribute")
                    {
                        var node = new ExtensionNode(assemblyName, type.FullName);
                        node.Path = (string)attr.ConstructorArguments[0].Value;
                        _extensions.Add(node);

                        var ep = GetExtensionPoint(node.Path);
                        if (ep != null) // TODO: Error message?
                            ep.Extensions.Add(node);
                    }
        }

        #endregion
    }
}
