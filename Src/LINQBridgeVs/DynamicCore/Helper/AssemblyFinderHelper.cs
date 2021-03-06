﻿#region License
// Copyright (c) 2013 Giovanni Campo
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
#endregion

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.InteropServices;
using LINQBridgeVs.Logging;

namespace LINQBridgeVs.DynamicCore.Helper
{
    internal static class AssemblyFinderHelper
    {
        private const string SearchPattern = "*{0}*.dll";
        private const int MaxDepth = 4;

        internal static readonly Func<string, bool> IsSystemAssembly =
            name => name.Contains("Microsoft") || name.Contains("System") || name.Contains("mscorlib");

        public static IFileSystem FileSystem { private get; set; }

        public static IEnumerable<string> GetReferencedAssembliesPath(this _Assembly assembly, string location, bool includeSystemAssemblies = false)
        {
            Log.Write("GetReferencedAssembliesPath Started - Parameters assembly: {0}, includeSystemAssemblies: {1}", assembly.ToString(), includeSystemAssemblies);
            var retPaths = new List<string>();

            var referencedAssemblies = assembly.GetReferencedAssemblies()
                                               .Where(name => includeSystemAssemblies || !IsSystemAssembly(name.Name))
                                               .Select(name => name.Name)
                                               .ToList();

            if (!referencedAssemblies.Any()) return retPaths;
            Log.Write(string.Format("There are {0} referenced non system assemblies", referencedAssemblies.Count));

            Log.Write(string.Format("Current Assembly is at location {0}", assembly.Location));

            var currentAssemblyPath = string.Empty;
            try
            {
                currentAssemblyPath = FileSystem.Path.GetDirectoryName(location);
            }
            catch (Exception exception)
            {
                Log.Write(exception, string.Format("GetDirectoryName of assembly: {0} failed. Path is wrong {1}", assembly.FullName, assembly.Location));
            }


            if (string.IsNullOrEmpty(currentAssemblyPath)) return Enumerable.Empty<string>();

            Log.Write("currentAssemblyPath: {0}", currentAssemblyPath);


            referencedAssemblies
                .ForEach(s =>
                             {
                                 Log.Write("Assembly {0} Located in {1} References Assembly {2} ", assembly.GetName().Name, assembly.Location, s);
                                 retPaths.Add(FindPath(s, currentAssemblyPath));
                             });


            return retPaths.Where(s => !string.IsNullOrEmpty(s));
        }

        internal static string FindPath(string fileToSearch, string rootPath, int depth = 0)
        {
            if (rootPath == null) return string.Empty;
            if (depth >= MaxDepth) return rootPath;

            try
            {

                Log.Write("SearchPattern: {0} in folder {1}", string.Format(SearchPattern, fileToSearch), rootPath);
                depth++;
                var file = FileSystem.Directory
                    .EnumerateFiles(rootPath, string.Format(SearchPattern, fileToSearch), System.IO.SearchOption.AllDirectories)
                    .AsParallel()
                    .OrderByDescending(info => FileSystem.FileInfo.FromFileName(info).LastAccessTime)
                    .FirstOrDefault();

                if (file == null)
                {
                    var parent = FileSystem.DirectoryInfo.FromDirectoryName(rootPath).Parent;
                    return parent != null ? FindPath(fileToSearch, parent.FullName, depth) : string.Empty;
                }


                Log.Write("File Found {0}", file);
                return file;
            }
            catch (Exception e)
            {
                Log.Write(e, "Exception in FindPath");
                throw;
            }


        }


    }
}
