﻿// Copyright (c) Ubisoft. All Rights Reserved.
// Licensed under the Apache 2.0 License. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Sharpmake.Generators.VisualStudio
{
    public partial class Androidproj : IProjectGenerator
    {
        public const string ProjectExtension = ".androidproj";
        private class GenerationContext : IVcxprojGenerationContext
        {
            #region IVcxprojGenerationContext implementation
            public Builder Builder { get; }
            public Project Project { get; }
            public Project.Configuration Configuration { get; internal set; }
            public string ProjectDirectory { get; }
            public DevEnv DevelopmentEnvironment => Configuration.Target.GetFragment<DevEnv>();
            public Options.ExplicitOptions Options
            {
                get
                {
                    Debug.Assert(_projectConfigurationOptions.ContainsKey(Configuration));
                    return _projectConfigurationOptions[Configuration];
                }
            }
            public IDictionary<string, string> CommandLineOptions { get; set; }
            public string ProjectDirectoryCapitalized { get; }
            public string ProjectSourceCapitalized { get; }
            public bool PlainOutput { get; }
            public void SelectOption(params Options.OptionAction[] options)
            {
                Sharpmake.Options.SelectOption(Configuration, options);
            }

            public void SelectOptionWithFallback(Action fallbackAction, params Options.OptionAction[] options)
            {
                Sharpmake.Options.SelectOptionWithFallback(Configuration, fallbackAction, options);
            }

            public string ProjectPath { get; }
            public string ProjectFileName { get; }
            public IReadOnlyList<Project.Configuration> ProjectConfigurations { get; }
            public IReadOnlyDictionary<Project.Configuration, Options.ExplicitOptions> ProjectConfigurationOptions => _projectConfigurationOptions;
            public DevEnvRange DevelopmentEnvironmentsRange { get; }
            public IReadOnlyDictionary<Platform, IPlatformVcxproj> PresentPlatforms { get; }
            public Resolver EnvironmentVariableResolver { get; internal set; }
            #endregion

            private Dictionary<Project.Configuration, Options.ExplicitOptions> _projectConfigurationOptions;

            public void SetProjectConfigurationOptions(Dictionary<Project.Configuration, Options.ExplicitOptions> projectConfigurationOptions)
            {
                _projectConfigurationOptions = projectConfigurationOptions;
            }

            internal AndroidPackageProject AndroidPackageProject { get; }

            public GenerationContext(Builder builder, string projectPath, Project project, IEnumerable<Project.Configuration> projectConfigurations)
            {
                Builder = builder;

                FileInfo fileInfo = new FileInfo(projectPath);
                ProjectPath = fileInfo.FullName;
                ProjectDirectory = Path.GetDirectoryName(ProjectPath);
                ProjectFileName = Path.GetFileName(ProjectPath);
                Project = project;
                AndroidPackageProject = (AndroidPackageProject)Project;

                ProjectDirectoryCapitalized = Util.GetCapitalizedPath(ProjectDirectory);
                ProjectSourceCapitalized = Util.GetCapitalizedPath(Project.SourceRootPath);

                ProjectConfigurations = VsUtil.SortConfigurations(projectConfigurations, Path.Combine(ProjectDirectoryCapitalized, ProjectFileName + ProjectExtension)).ToArray();
                DevelopmentEnvironmentsRange = new DevEnvRange(ProjectConfigurations);

                PresentPlatforms = ProjectConfigurations.Select(conf => conf.Platform).Distinct().ToDictionary(p => p, p => PlatformRegistry.Get<IPlatformVcxproj>(p));
            }

            public void Reset()
            {
                CommandLineOptions = null;
                Configuration = null;
                EnvironmentVariableResolver = null;
            }
        }

        // The default value used by the Ant build type is to remove the AndroidBuildType tag
        private string _androidBuildType = FileGeneratorUtilities.RemoveLineTag;
        private bool _isGradleBuild { get { return _androidBuildType.Equals("Gradle", StringComparison.InvariantCultureIgnoreCase); } }

        public void Generate(
            Builder builder,
            Project project,
            List<Project.Configuration> configurations,
            string projectFile,
            List<string> generatedFiles,
            List<string> skipFiles)
        {
            if (!(project is AndroidPackageProject))
                throw new ArgumentException("Project is not a AndroidPackageProject");

            var context = new GenerationContext(builder, projectFile, project, configurations);
            GenerateImpl(context, generatedFiles, skipFiles);
        }

        private void GenerateConfOptions(GenerationContext context)
        {
            // generate all configuration options once...
            var projectOptionsGen = new ProjectOptionsGenerator();
            var projectConfigurationOptions = new Dictionary<Project.Configuration, Options.ExplicitOptions>();
            context.SetProjectConfigurationOptions(projectConfigurationOptions);
            foreach (Project.Configuration conf in context.ProjectConfigurations)
            {
                context.Configuration = conf;

                // set generator information
                var platformVcxproj = context.PresentPlatforms[conf.Platform];
                var configurationTasks = PlatformRegistry.Get<Project.Configuration.IConfigurationTasks>(conf.Platform);
                conf.GeneratorSetOutputFullExtensions(
                    platformVcxproj.ExecutableFileFullExtension,
                    platformVcxproj.PackageFileFullExtension,
                    configurationTasks.GetDefaultOutputFullExtension(Project.Configuration.OutputType.Dll),
                    platformVcxproj.ProgramDatabaseFileFullExtension);

                projectConfigurationOptions.Add(conf, new Options.ExplicitOptions());
                context.CommandLineOptions = new ProjectOptionsGenerator.VcxprojCmdLineOptions();

                projectOptionsGen.GenerateOptions(context);
                GenerateOptions(context);

                context.Reset(); // just a safety, not necessary to clean up
            }
        }

        private void GenerateImpl(
            GenerationContext context,
            List<string> generatedFiles,
            List<string> skipFiles)
        {
            GenerateConfOptions(context);

            var fileGenerator = new XmlFileGenerator();

            // xml begin header
            string toolsVersion = context.DevelopmentEnvironmentsRange.MinDevEnv.GetVisualProjectToolsVersionString();
            using (fileGenerator.Declare("toolsVersion", toolsVersion))
                fileGenerator.Write(Template.Project.ProjectBegin);

            VsProjCommon.WriteCustomProperties(context.Project.CustomProperties, fileGenerator);

            VsProjCommon.WriteProjectConfigurationsDescription(context.ProjectConfigurations, fileGenerator);

            // xml end header

            string androidTargetsPath = Options.GetConfOption<Options.Android.General.AndroidTargetsPath>(context.ProjectConfigurations, rootpath: context.ProjectDirectoryCapitalized);

            var firstConf = context.ProjectConfigurations.First();
            _androidBuildType = Options.GetOptionValue("androidBuildType", context.ProjectConfigurationOptions.Values, FileGeneratorUtilities.RemoveLineTag);

            using (fileGenerator.Declare("androidBuildType", _androidBuildType))
            using (fileGenerator.Declare("projectName", firstConf.ProjectName))
            using (fileGenerator.Declare("guid", firstConf.ProjectGuid))
            using (fileGenerator.Declare("toolsVersion", toolsVersion))
            using (fileGenerator.Declare("androidTargetsPath", Util.EnsureTrailingSeparator(androidTargetsPath)))
            {
                fileGenerator.Write(Template.Project.ProjectDescription);
            }

            fileGenerator.Write(VsProjCommon.Template.PropertyGroupEnd);

            foreach (var platform in context.PresentPlatforms.Values)
                platform.GeneratePlatformSpecificProjectDescription(context, fileGenerator);

            fileGenerator.Write(Template.Project.ImportAndroidDefaultProps);

            foreach (var platform in context.PresentPlatforms.Values)
                platform.GeneratePostDefaultPropsImport(context, fileGenerator);

            // configuration general
            foreach (Project.Configuration conf in context.ProjectConfigurations)
            {
                context.Configuration = conf;

                using (fileGenerator.Declare("platformName", Util.GetToolchainPlatformString(conf.Platform, conf.Project, conf.Target)))
                using (fileGenerator.Declare("conf", conf))
                using (fileGenerator.Declare("options", context.ProjectConfigurationOptions[conf]))
                {
                    fileGenerator.Write(Template.Project.ProjectConfigurationsGeneral);
                }
            }

            // .props files
            fileGenerator.Write(Template.Project.ProjectAfterConfigurationsGeneral);

            VsProjCommon.WriteProjectCustomPropsFiles(context.Project.CustomPropsFiles, context.ProjectDirectoryCapitalized, fileGenerator);
            VsProjCommon.WriteConfigurationsCustomPropsFiles(context.ProjectConfigurations, context.ProjectDirectoryCapitalized, fileGenerator);

            fileGenerator.Write(Template.Project.ProjectAfterImportedProps);

            string androidPackageDirectory = context.AndroidPackageProject.AntBuildRootDirectory;

            if (!_isGradleBuild)
            {
                // configuration ItemDefinitionGroup
                foreach (Project.Configuration conf in context.ProjectConfigurations)
                {
                    context.Configuration = conf;

                    using (fileGenerator.Declare("platformName", Util.GetToolchainPlatformString(conf.Platform, conf.Project, conf.Target)))
                    using (fileGenerator.Declare("conf", conf))
                    using (fileGenerator.Declare("options", context.ProjectConfigurationOptions[conf]))
                    using (fileGenerator.Declare("androidPackageDirectory", androidPackageDirectory))
                    {
                        fileGenerator.Write(Template.Project.ProjectConfigurationBeginItemDefinition);
                        {
                                fileGenerator.Write(Template.Project.AntPackage);
                        }
                        fileGenerator.Write(Template.Project.ProjectConfigurationEndItemDefinition);
                    }
                }
            }


            // Generate Build.Bat for Gradle
            var RemoveLineTag = FileGeneratorUtilities.RemoveLineTag;
            var options = context.Options;
            var project_conf = firstConf;
            context.SelectOption(
            Options.Option(Options.Android.General.AndroidAPILevel.Latest, () =>
                {
                    string lookupDirectory = options["androidHome"];

                    // Android API Level
                    string androidApiLevel = RemoveLineTag;
                    string androidBuildToolVersion = RemoveLineTag;

                    if (lookupDirectory != RemoveLineTag)
                    {
                        string latestApiLevel = FindLatestApiLevelInDirectory(Path.Combine(lookupDirectory, "platforms"));
                        if (!string.IsNullOrEmpty(latestApiLevel))
                            androidApiLevel = latestApiLevel;

                        string latestBuildTool = FindLatestBuildToolsInDirectory(Path.Combine(lookupDirectory, "build-tools"));
                        if (!string.IsNullOrEmpty(latestBuildTool))
                            androidBuildToolVersion = latestBuildTool;
                    }
                    options["AndroidAPILevel"] = androidApiLevel;
                    options["AndroidBuildToolVersion"] = androidBuildToolVersion;
                }
            ),
            Options.Option(Options.Android.General.AndroidAPILevel.Default,   () => { options["AndroidAPILevel"] = RemoveLineTag; }),
            Options.Option(Options.Android.General.AndroidAPILevel.Android16, () => { options["AndroidAPILevel"] = "android-16"; }),
            Options.Option(Options.Android.General.AndroidAPILevel.Android17, () => { options["AndroidAPILevel"] = "android-17"; }),
            Options.Option(Options.Android.General.AndroidAPILevel.Android18, () => { options["AndroidAPILevel"] = "android-18"; }),
            Options.Option(Options.Android.General.AndroidAPILevel.Android19, () => { options["AndroidAPILevel"] = "android-19"; }),
            Options.Option(Options.Android.General.AndroidAPILevel.Android20, () => { options["AndroidAPILevel"] = "android-20"; }),
            Options.Option(Options.Android.General.AndroidAPILevel.Android21, () => { options["AndroidAPILevel"] = "android-21"; }),
            Options.Option(Options.Android.General.AndroidAPILevel.Android22, () => { options["AndroidAPILevel"] = "android-22"; }),
            Options.Option(Options.Android.General.AndroidAPILevel.Android23, () => { options["AndroidAPILevel"] = "android-23"; }),
            Options.Option(Options.Android.General.AndroidAPILevel.Android24, () => { options["AndroidAPILevel"] = "android-24"; }),
            Options.Option(Options.Android.General.AndroidAPILevel.Android25, () => { options["AndroidAPILevel"] = "android-25"; }),
            Options.Option(Options.Android.General.AndroidAPILevel.Android26, () => { options["AndroidAPILevel"] = "android-26"; }),
            Options.Option(Options.Android.General.AndroidAPILevel.Android27, () => { options["AndroidAPILevel"] = "android-27"; }),
            Options.Option(Options.Android.General.AndroidAPILevel.Android28, () => { options["AndroidAPILevel"] = "android-28"; }),
            Options.Option(Options.Android.General.AndroidAPILevel.Android29, () => { options["AndroidAPILevel"] = "android-29"; }),
            Options.Option(Options.Android.General.AndroidAPILevel.Android30, () => { options["AndroidAPILevel"] = "android-30"; })
            );

            string assetPaths = firstConf.ResourceIncludePaths.Count() == 0 ? RemoveLineTag : "";
            foreach(var assetPath in firstConf.ResourceIncludePaths)
            {
                assetPaths += "-A " +  assetPath;
            }

            FileGenerator gradleAlternateBuild = new FileGenerator();
            using(gradleAlternateBuild.Declare("sdkRoot", options["androidHome"]))
            using(gradleAlternateBuild.Declare("javaHome", options["javaHome"]))
            using(gradleAlternateBuild.Declare("apiVersion", options["AndroidAPILevel"]))
            using(gradleAlternateBuild.Declare("buildToolsVersion", options["AndroidBuildToolVersion"]))
            using(gradleAlternateBuild.Declare("androidManifestPath", context.AndroidPackageProject.AndroidManifest))
            using(gradleAlternateBuild.Declare("outputPath", Path.Combine(project_conf.TargetPath, project_conf.TargetFileName + ".apk")))
            using(gradleAlternateBuild.Declare("assetPaths", assetPaths))
            {
                gradleAlternateBuild.Write(Template.Project.GradleBuildBat);
            }

            FileInfo build_bat = new FileInfo(context.ProjectPath + ".bat");
            if(context.Builder.Context.WriteGeneratedFile(null, build_bat, gradleAlternateBuild))
            {
                generatedFiles.Add(build_bat.FullName);
            }
            else
            {
                skipFiles.Add(build_bat.FullName);
            }

            if (_isGradleBuild)
            {
                using (fileGenerator.Declare("gradlePlugin", context.AndroidPackageProject.GradlePlugin))
                using (fileGenerator.Declare("gradleVersion", context.AndroidPackageProject.GradleVersion))
                using (fileGenerator.Declare("gradleAppLibName", context.AndroidPackageProject.GradleAppLibName))
                using (fileGenerator.Declare("toolName", build_bat.FullName))
                {
                    fileGenerator.Write(VsProjCommon.Template.ItemDefinitionGroupBegin);
                    fileGenerator.Write(Template.Project.GradlePackage);
                    fileGenerator.Write(VsProjCommon.Template.ItemDefinitionGroupEnd);
                }
            }

            GenerateFilesSection(context, fileGenerator);

            // .targets
            fileGenerator.Write(Template.Project.ProjectTargets);

            GenerateProjectReferences(context, fileGenerator);

            // Environment variables
            var environmentVariables = context.ProjectConfigurations.Select(conf => conf.Platform).Distinct().SelectMany(platform => context.PresentPlatforms[platform].GetEnvironmentVariables(context));
            VsProjCommon.WriteEnvironmentVariables(environmentVariables, fileGenerator);

            fileGenerator.Write(Template.Project.ProjectEnd);

            // remove all line that contain RemoveLineTag
            fileGenerator.RemoveTaggedLines();

            FileInfo projectFileInfo = new FileInfo(context.ProjectPath + ProjectExtension);
            if (context.Builder.Context.WriteGeneratedFile(context.Project.GetType(), projectFileInfo, fileGenerator))
                generatedFiles.Add(projectFileInfo.FullName);
            else
                skipFiles.Add(projectFileInfo.FullName);


            
        }

        private void GenerateFilesSection(
            GenerationContext context,
            IFileGenerator fileGenerator)
        {
            Strings projectFiles = context.Project.GetSourceFilesForConfigurations(context.ProjectConfigurations);

            // Add source files
            var allFiles = new List<Vcxproj.ProjectFile>();
            var includeFiles = new List<Vcxproj.ProjectFile>();
            var sourceFiles = new List<Vcxproj.ProjectFile>();
            var contentFiles = new List<Vcxproj.ProjectFile>();

            foreach (string file in projectFiles)
            {
                var projectFile = new Vcxproj.ProjectFile(context, file);
                allFiles.Add(projectFile);
            }

            allFiles.Sort((l, r) => { return string.Compare(l.FileNameProjectRelative, r.FileNameProjectRelative, StringComparison.InvariantCultureIgnoreCase); });

            // type -> files
            var customSourceFiles = new Dictionary<string, List<Vcxproj.ProjectFile>>();
            foreach (var projectFile in allFiles)
            {
                string type = null;
                if (context.Project.ExtensionBuildTools.TryGetValue(projectFile.FileExtension, out type))
                {
                    List<Vcxproj.ProjectFile> files = null;
                    if (!customSourceFiles.TryGetValue(type, out files))
                    {
                        files = new List<Vcxproj.ProjectFile>();
                        customSourceFiles[type] = files;
                    }
                    files.Add(projectFile);
                }
                else if (context.Project.SourceFilesCompileExtensions.Contains(projectFile.FileExtension) ||
                         (string.Compare(projectFile.FileExtension, ".rc", StringComparison.OrdinalIgnoreCase) == 0))
                {
                    sourceFiles.Add(projectFile);
                }
                else if (string.Compare(projectFile.FileExtension, ".h", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    includeFiles.Add(projectFile);
                }
                else
                {
                    contentFiles.Add(projectFile);
                }
            }

            // Write header files
            if (includeFiles.Count > 0)
            {
                fileGenerator.Write(Template.Project.ProjectFilesBegin);
                foreach (var file in includeFiles)
                {
                    using (fileGenerator.Declare("file", file))
                        fileGenerator.Write(Template.Project.ProjectFilesHeader);
                }
                fileGenerator.Write(Template.Project.ProjectFilesEnd);
            }

            // Write content files
            if (contentFiles.Count > 0)
            {
                fileGenerator.Write(Template.Project.ProjectFilesBegin);
                foreach (var file in contentFiles)
                {
                    using (fileGenerator.Declare("file", file))
                        fileGenerator.Write(Template.Project.ContentSimple);
                }
                fileGenerator.Write(Template.Project.ProjectFilesEnd);
            }

            // Write Android project files
            fileGenerator.Write(Template.Project.ItemGroupBegin);

            if (_isGradleBuild)
            {
                foreach (var file in context.AndroidPackageProject.GradleTemplateFiles)
                {
                    using (fileGenerator.Declare("gradleTemplateFile", file))
                        fileGenerator.Write(Template.Project.GradleTemplate);
                }
            }
            else
            {
                using (fileGenerator.Declare("antBuildXml", context.AndroidPackageProject.AntBuildXml))
                using (fileGenerator.Declare("antProjectPropertiesFile", context.AndroidPackageProject.AntProjectPropertiesFile))
                using (fileGenerator.Declare("androidManifest", context.AndroidPackageProject.AndroidManifest))
                {
                    fileGenerator.Write(Template.Project.AntBuildXml);
                    fileGenerator.Write(Template.Project.AndroidManifest);
                    fileGenerator.Write(Template.Project.AntProjectPropertiesFile);
                }
            }

            fileGenerator.Write(Template.Project.ItemGroupEnd);
        }

        private struct ProjectDependencyInfo
        {
            public string ProjectFullFileNameWithExtension;
            public string ProjectGuid;
        }

        private void GenerateProjectReferences(
            GenerationContext context,
            IFileGenerator fileGenerator)
        {
            var dependencies = new UniqueList<ProjectDependencyInfo>();
            foreach (var c in context.ProjectConfigurations)
            {
                foreach (var d in c.ConfigurationDependencies)
                {
                    // Ignore projects marked as Export
                    if (d.Project.SharpmakeProjectType == Project.ProjectTypeAttribute.Export)
                        continue;

                    ProjectDependencyInfo depInfo;
                    depInfo.ProjectFullFileNameWithExtension = d.ProjectFullFileNameWithExtension;

                    if (d.Project.SharpmakeProjectType != Project.ProjectTypeAttribute.Compile)
                        depInfo.ProjectGuid = d.ProjectGuid;
                    else
                        throw new NotImplementedException("Sharpmake.Compile not supported as a dependency by this generator.");
                    dependencies.Add(depInfo);
                }
            }

            if (dependencies.Count > 0)
            {
                fileGenerator.Write(Template.Project.ItemGroupBegin);
                foreach (var d in dependencies)
                {
                    string include = Util.PathGetRelative(context.ProjectDirectory, d.ProjectFullFileNameWithExtension);
                    using (fileGenerator.Declare("include", include))
                    using (fileGenerator.Declare("projectGUID", d.ProjectGuid))
                    {
                        fileGenerator.Write(Template.Project.ProjectReference);
                    }
                }
                fileGenerator.Write(Template.Project.ItemGroupEnd);
            }
        }

        private void GenerateOptions(GenerationContext context)
        {
            var options = context.Options;
            var conf = context.Configuration;

            //OutputFile ( APK File )
            options["OutputFile"] = conf.TargetFileFullName;

            //AndroidAppLibName Native Library Packaged into the APK
            options["AndroidAppLibName"] = FileGeneratorUtilities.RemoveLineTag;
            if (context.AndroidPackageProject.AppLibType != null)
            {
                Project.Configuration appLibConf = conf.ConfigurationDependencies.FirstOrDefault(confDep => (confDep.Project.GetType() == context.AndroidPackageProject.AppLibType));
                if (appLibConf != null)
                {
                    // The lib name to first load from an AndroidActivity must be a dynamic library.
                    if (appLibConf.Output != Project.Configuration.OutputType.Dll)
                        throw new Error("Cannot use configuration \"{0}\" as app lib for package configuration \"{1}\". Output type must be set to dynamic library.", appLibConf, conf);

                    options["AndroidAppLibName"] = appLibConf.TargetFilePrefix + appLibConf.TargetFileName + appLibConf.TargetFileSuffix;
                }
                else
                {
                    throw new Error("Missing dependency of type \"{0}\" in configuration \"{1}\" dependencies.", context.AndroidPackageProject.AppLibType.ToNiceTypeName(), conf);
                }
            }
            //OutputDirectory
            //    The debugger need a rooted path to work properly.
            //    So we root the relative output directory to $(ProjectDir) to work around this limitation.
            //    Hopefully in a future version of the cross platform tools will be able to remove this hack.
            string outputDirectoryRelative = Util.PathGetRelative(context.ProjectDirectoryCapitalized, conf.TargetPath);
            options["OutputDirectory"] = outputDirectoryRelative;

            //IntermediateDirectory
            string intermediateDirectoryRelative = Util.PathGetRelative(context.ProjectDirectoryCapitalized, conf.IntermediatePath);
            options["IntermediateDirectory"] = intermediateDirectoryRelative;
        }

        // will find folders named after the platform api level,
        // following this pattern: android-XX, with XX being 2 digits
        public static string FindLatestApiLevelInDirectory(string directory)
        {
            string latestDirectory = null;
            if (Directory.Exists(directory))
            {
                var androidDirectories = Sharpmake.Util.DirectoryGetDirectories(directory);
                int latestValue = 0;
                foreach (var folderName in androidDirectories.Select(Path.GetFileName))
                {
                    int current = 0;
                    if (TryParseAndroidApiValue(folderName, out current))
                    {
                        if (current > latestValue)
                        {
                            latestValue = current;
                            latestDirectory = folderName;
                        }
                    }
                }
            }

            return latestDirectory;
        }

        public static bool TryParseAndroidApiValue(string apiString, out int apiValue)
        {
            apiValue = 0;
            if (string.IsNullOrWhiteSpace(apiString))
                return false;

            const int devKitEditionTargetExpectedLength = 10;
            if (apiString.Length != devKitEditionTargetExpectedLength)
                return false;

            // skip 'android-'
            string valueString = apiString.Substring(8);

            return int.TryParse(valueString, out apiValue);
        }


        // will try to find the latest build tools version
        // following this pattern: XX.YY.ZZ XX => major version, YY => minor version, ZZ => subverison number
        public static string FindLatestBuildToolsInDirectory(string directory)
        {
            string latestDirectory = null;
            if (Directory.Exists(directory))
            {
                var androidDirectories = Sharpmake.Util.DirectoryGetDirectories(directory);

                int latestMajorValue = 0;
                int latestMinorValue = 0;
                int latestSubminorValue = 0;

                foreach (var folderName in androidDirectories.Select(Path.GetFileName))
                {
                    int currentMajor = 0;
                    int currentMinor = 0;
                    int currentSubminor = 0;

                    if (TryParseAndroidBuildToolsValue(folderName, out currentMajor, out currentMinor, out currentSubminor))
                    {
                        if (currentMajor >= latestMajorValue && currentMinor >= latestMajorValue && currentSubminor >= latestSubminorValue)
                        {
                            latestMajorValue = currentMajor;
                            latestMinorValue = currentMinor;
                            latestSubminorValue = currentSubminor;
                            latestDirectory = folderName;
                        }
                    }
                }
            }

            return latestDirectory;
        }

        public static bool TryParseAndroidBuildToolsValue(string apiString, out int apiMajorValue, out int apiMinorValue, out int apiSubminorValue)
        {
            apiMajorValue = 0;
            apiMinorValue = 0;
            apiSubminorValue = 0;

            if (string.IsNullOrWhiteSpace(apiString))
                return false;

            string[] valueStrings = apiString.Split(".");

            bool res = int.TryParse(valueStrings[0], out apiMajorValue);
            res = res && int.TryParse(valueStrings[1], out apiMinorValue);
            res = res && int.TryParse(valueStrings[2], out apiSubminorValue);

            return res;
        }

    }
}
