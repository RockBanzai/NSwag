//-----------------------------------------------------------------------
// <copyright file="IsolatedCommandBase.cs" company="NSwag">
//     Copyright (c) Rico Suter. All rights reserved.
// </copyright>
// <license>https://github.com/NSwag/NSwag/blob/master/LICENSE.md</license>
// <author>Rico Suter, mail@rsuter.com</author>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NConsole;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NSwag.Annotations;
using NSwag.AssemblyLoader;
using NSwag.AssemblyLoader.Utilities;
using NSwag.SwaggerGeneration;
using NSwag.SwaggerGeneration.WebApi;

namespace NSwag.Commands
{
    /// <summary>A command which is run in isolation.</summary>
    public abstract class IsolatedCommandBase<TResult> : IConsoleCommand
    {
        [Argument(Name = "Assembly", IsRequired = false, Description = "The path or paths to the .NET assemblies (comma separated).")]
        public string[] AssemblyPaths { get; set; } = new string[0];

        [Argument(Name = "AssemblyConfig", IsRequired = false, Description = "The path to the assembly App.config or Web.config (optional).")]
        public string AssemblyConfig { get; set; }

        [Argument(Name = "ReferencePaths", IsRequired = false, Description = "The paths to search for referenced assembly files (comma separated).")]
        public string[] ReferencePaths { get; set; } = new string[0];

        public abstract Task<object> RunAsync(CommandLineProcessor processor, IConsoleHost host);

        protected async Task<TResult> RunIsolatedAsync(string configurationFile)
        {
            var assemblyDirectory = AssemblyPaths.Any() ? Path.GetDirectoryName(Path.GetFullPath(PathUtilities.ExpandFileWildcards(AssemblyPaths).First())) : configurationFile;
            var bindingRedirects = GetBindingRedirects();
            var assemblies = GetAssemblies(assemblyDirectory);

            using (var isolated = new AppDomainIsolation<IsolatedCommandAssemblyLoader<TResult>>(assemblyDirectory, AssemblyConfig, bindingRedirects, assemblies))
            {
                return await Task.Run(() => isolated.Object.Run(GetType().FullName, JsonConvert.SerializeObject(this), AssemblyPaths, ReferencePaths)).ConfigureAwait(false);
            }
        }

        protected abstract Task<TResult> RunIsolatedAsync(AssemblyLoader.AssemblyLoader assemblyLoader);

        private class IsolatedCommandAssemblyLoader<TResult> : AssemblyLoader.AssemblyLoader
        {
            internal TResult Run(string commandType, string commandData, string[] assemblyPaths, string[] referencePaths)
            {
                RegisterReferencePaths(GetAllReferencePaths(assemblyPaths, referencePaths));

                var type = Type.GetType(commandType);
                var command = (IsolatedCommandBase<TResult>)JsonConvert.DeserializeObject(commandData, type);

                return command.RunIsolatedAsync(this).GetAwaiter().GetResult();
            }
        }

        private static string[] GetAllReferencePaths(string[] assemblyPaths, string[] referencePaths)
        {
            return assemblyPaths.Select(p => Path.GetDirectoryName(PathUtilities.MakeAbsolutePath(p, Directory.GetCurrentDirectory())))
                .Concat(referencePaths)
                .Distinct()
                .ToArray();
        }

        public IEnumerable<string> GetAssemblies(string assemblyDirectory)
        {
            var codeBaseDirectory = Path.GetDirectoryName(typeof(IsolatedSwaggerOutputCommandBase).GetTypeInfo()
                .Assembly.CodeBase.Replace("file:///", string.Empty));

            yield return codeBaseDirectory + "/Newtonsoft.Json.dll";
            yield return codeBaseDirectory + "/NJsonSchema.dll";
            yield return codeBaseDirectory + "/NSwag.Core.dll";
            yield return codeBaseDirectory + "/NSwag.Commands.dll";
            yield return codeBaseDirectory + "/NSwag.SwaggerGeneration.dll";
            yield return codeBaseDirectory + "/NSwag.SwaggerGeneration.WebApi.dll";
        }

        public IEnumerable<BindingRedirect> GetBindingRedirects()
        {
#if NET451
            yield return new BindingRedirect("Newtonsoft.Json", typeof(JToken), "30ad4fe6b2a6aeed");
            yield return new BindingRedirect("NJsonSchema", typeof(JsonSchema4), "c2f9c3bdfae56102");
            yield return new BindingRedirect("NSwag.Core", typeof(SwaggerDocument), "c2d88086e098d109");
            yield return new BindingRedirect("NSwag.SwaggerGeneration", typeof(SwaggerJsonSchemaGenerator), "c2d88086e098d109");
            yield return new BindingRedirect("NSwag.SwaggerGeneration.WebApi", typeof(WebApiToSwaggerGenerator), "c2d88086e098d109");
            yield return new BindingRedirect("NSwag.Annotations", typeof(SwaggerTagsAttribute), "c2d88086e098d109");
            yield return new BindingRedirect("System.Runtime", "4.0.0.0", "b03f5f7f11d50a3a");
#else
            return new BindingRedirect[0];
#endif
        }
    }
}