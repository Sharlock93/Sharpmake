// Copyright (c) Ubisoft. All Rights Reserved.
// Licensed under the Apache 2.0 License. See LICENSE.md in the project root for license information.

using Sharpmake.Generators;
using Sharpmake.Generators.VisualStudio;

namespace Sharpmake.PSVita
{
    public static partial class PSVita
    {

        [PlatformImplementation(Platform.psvita,
                    typeof(IPlatformDescriptor),
                    typeof(Project.Configuration.IConfigurationTasks),
                    typeof(IPlatformVcxproj))]
        public sealed partial class PSVitaPlatform : BasePlatform, IPlatformDescriptor, Project.Configuration.IConfigurationTasks
        {
            #region IPlatformDescriptor
            public override string SimplePlatformString => "PSVita";

            public override bool IsMicrosoftPlatform => false;

            public override bool IsPcPlatform => false;

            public override bool IsUsingClang => true;

            public override bool IsLinkerInvokedViaCompiler { get; set; } = false;

            public override bool HasDotNetSupport => false;

            public override bool HasSharedLibrarySupport => true;

            public override EnvironmentVariableResolver GetPlatformEnvironmentResolver(params VariableAssignment[] parameters)
            {
                return new PSVitaEnvironmentResolver(parameters);
            }
            #endregion


            #region IPlatformVcxproj
            public override string ExecutableFileFullExtension => ".self";

            public override string SharedLibraryFileFullExtension => ".sprx";

            public override string StaticLibraryFileFullExtension => ".a";

            public override string ProgramDatabaseFileFullExtension => string.Empty;

            public override void SetupPlatformToolsetOptions(IGenerationContext context)
            {
                context.Options["PlatformToolset"] = "SNC";
            }

            public override void GenerateProjectCompileVcxproj(IVcxprojGenerationContext context, IFileGenerator generator)
            {
                generator.Write(_projectConfigurationTemplate);
            }

            #endregion

            #region IConfigurationTasks
            public string GetDefaultOutputFullExtension(Project.Configuration.OutputType output)
            {
                switch (output)
                {
                    case Project.Configuration.OutputType.Exe:
                        return ".self";
                    case Project.Configuration.OutputType.Dll:
                        return ".sprx";
                    default:
                        return ".a";
                }

            }
            public string GetOutputFileNamePrefix(Project.Configuration.OutputType outputType)
            {
                return string.Empty;
            }

            public IEnumerable<string> GetPlatformLibraryPaths(Project.Configuration configuration)
            {
                return Enumerable.Empty<string>();
            }

            public void SetupDynamicLibraryPaths(Project.Configuration configuration, DependencySetting dependencySetting, Project.Configuration dependency)
            {
                Console.WriteLine("Dynamic Library Path");
            }

            public void SetupStaticLibraryPaths(Project.Configuration configuration, DependencySetting dependencySetting, Project.Configuration dependency)
            {
                Console.WriteLine("Setting up static library");
            }


            #endregion
        }

    }
}
