// Copyright (c) Ubisoft. All Rights Reserved.
// Licensed under the Apache 2.0 License. See LICENSE.md in the project root for license information.

using Sharpmake.Generators;
using Sharpmake.Generators.FastBuild;
using Sharpmake.Generators.VisualStudio;

namespace Sharpmake.PSVita
{
    public static partial class PSVita
    {

        [PlatformImplementation(Platform.psvita,
            typeof(IPlatformDescriptor),
            typeof(Project.Configuration.IConfigurationTasks),
            typeof(IPlatformVcxproj),
            typeof(IPlatformBff),
            typeof(IFastBuildCompilerSettings))]
            public sealed partial class PSVitaPlatform : BasePlatform, IPlatformDescriptor, Project.Configuration.IConfigurationTasks, IFastBuildCompilerSettings
        {
        #region IPlatformDescriptor
            public override string SimplePlatformString => "PSVita";

            public override bool IsMicrosoftPlatform => false;

            public override bool IsPcPlatform => false;

            public override bool IsUsingClang => false;

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
                // Console.WriteLine("Dynamic Library Path");
            }

            public void SetupStaticLibraryPaths(Project.Configuration configuration, DependencySetting dependencySetting, Project.Configuration dependency)
            {
                // Console.WriteLine("Setting up static library");
            }

            public override string GetToolchainPlatformString(ITarget target)
            {
                return "PSVita";
            }
            #endregion

        #region IFastBuildCompilerSettings

            public IDictionary<DevEnv, string> BinPath { get; set; } = new Dictionary<DevEnv, string>();
            public IDictionary<IFastBuildCompilerKey, CompilerFamily> CompilerFamily { get; set; } = new Dictionary<IFastBuildCompilerKey, CompilerFamily>();
            public IDictionary<DevEnv, string> LinkerPath { get; set; } = new Dictionary<DevEnv, string>();
            public IDictionary<DevEnv, string> LinkerExe { get; set; } = new Dictionary<DevEnv, string>();
            public IDictionary<DevEnv, bool> LinkerInvokedViaCompiler { get; set; } = new Dictionary<DevEnv, bool>();
            public IDictionary<DevEnv, string> LibrarianExe { get; set; } = new Dictionary<DevEnv, string>();
            public IDictionary<DevEnv, Strings> ExtraFiles { get; set; } = new Dictionary<DevEnv, Strings>();

            #endregion


            public override string BffPlatformDefine => "__psp2__";

            public override string CConfigName(Configuration conf) {
                return ".psvitaConfig";
            }
            
            public override string CppConfigName(Configuration conf) {
                return ".psvitaPPConfig";
            }

            public override void SetupExtraLinkerSettings(IFileGenerator fileGenerator, Project.Configuration configuration, string fastBuildOutputFile)
            {
                fileGenerator.Write(_linkerOptionsTemplate);
            }

           
            public override IEnumerable<Project.Configuration.BuildStepBase> GetExtraPostBuildEvents(Project.Configuration configuration, string fastBuildOutputFile) {
                return new List<Project.Configuration.BuildStepExecutable>();
            }

            
            public override IEnumerable<Project.Configuration.BuildStepExecutable> GetExtraStampEvents(Project.Configuration configuration, string fastBuildOutputFile)
            {
                return new List<Project.Configuration.BuildStepExecutable>();
            }

            /// <summary>
            /// Get the linker output name for this platform.
            /// </summary>
            /// <param name="outputType">The project output type</param>
            /// <param name="fastBuildOutputFile">The original file name of the build output.</param>
            /// <returns>The final file name of the build output.</returns>

            public override void AddCompilerSettings(IDictionary<string, CompilerSettings> masterCompilerSettings, Project.Configuration conf)
            {
                var platform = conf.Target.GetFragment<Platform>();
                var dev_env = conf.Target.GetFragment<DevEnv>();
                string executablePath = $"{PSVitaEnvironmentResolver.SCE_PSP2_SDK_DIR}\\host_tools\\build\\bin";

                var executableCompilerName = "$ExecutableRootPath$\\psp2snc.exe";

                CompilerSettings settings;

                string compiler_name = $"Compiler-{Util.GetToolchainPlatformString(platform, conf.Target)}-{dev_env}";

                if(!masterCompilerSettings.ContainsKey(compiler_name)) {
                    var compiler_config = new Dictionary<string, CompilerSettings.Configuration>();

                    compiler_config.Add(
                        CConfigName(conf),
                        new CompilerSettings.Configuration(
                            Platform.psvita,
                            compiler : compiler_name,
                            binPath : executablePath,
                            linkerPath : executablePath,
                            linker: "$LinkerPath$\\psp2ld.exe",
                            fastBuildLinkerType: CompilerSettings.LinkerType.ClangOrbis
                    ));

                    Strings s = new Strings();
                    settings = new CompilerSettings(compiler_name, Sharpmake.CompilerFamily.GCC, Platform.psvita, s, executableCompilerName, executablePath, DevEnv.vs2022, compiler_config);
                    masterCompilerSettings.Add(compiler_name, settings);
                }

            }

            public void SetupClangOptions(IFileGenerator generator)
            {
                generator.Write(_compilerExtraOptions);
                generator.Write(_compilerOptimizationOptions);
            }
        }

    
    }
}
