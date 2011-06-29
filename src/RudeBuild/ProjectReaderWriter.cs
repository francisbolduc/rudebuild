using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using System.Xml;

namespace RudeBuild
{
    internal abstract class SingleProjectReaderWriterBase
    {
        protected Settings _settings;

        public SingleProjectReaderWriterBase(Settings settings)
        {
            _settings = settings;
        }

        public abstract void ReadWrite(string projectFileName, SolutionInfo solutionInfo, XDocument projectDocument, XNamespace ns);
    }

    internal class SingleProjectReaderWriterVS2010 : SingleProjectReaderWriterBase
    {
        public SingleProjectReaderWriterVS2010(Settings settings)
            : base(settings)
        {
        }

        private XElement GetConfigurationElement(XDocument projectDocument, XNamespace ns)
        {
            string configCondition = "'$(Configuration)|$(Platform)'=='" + _settings.BuildOptions.Config + "'";

            var configElements =
                from configElement in projectDocument.Descendants(ns + "ItemDefinitionGroup")
                where configElement.Attribute("Condition") != null && configElement.Attribute("Condition").Value == configCondition
                select configElement;
            return configElements.SingleOrDefault();
        }

        private string GetPrecompiledHeader(XDocument projectDocument, XNamespace ns)
        {
            XElement configElement = GetConfigurationElement(projectDocument, ns);
            if (null != configElement)
            {
                XElement precompiledHeaderElement = configElement.Descendants(ns + "PrecompiledHeader").SingleOrDefault();
                XElement precompiledHeaderFileElement = configElement.Descendants(ns + "PrecompiledHeaderFile").SingleOrDefault();
                if (null != precompiledHeaderElement && precompiledHeaderElement.Value == "Use")
                {
                    if (null != precompiledHeaderFileElement)
                        return precompiledHeaderFileElement.Value;
                    else
                        return "StdAfx.h";
                }
            }

            return string.Empty;
        }

        private void DisablePrecompiledHeaders(XDocument projectDocument, XNamespace ns)
        {
            var pchElements =
                from pchElement in projectDocument.Descendants(ns + "PrecompiledHeader")
                select pchElement;
            foreach (XElement pchElement in pchElements)
            {
                pchElement.Value = "NotUsing";
            }
        }

        private XElement GetCompileItemGroupElement(string projectFileName, XNamespace ns, XDocument projectDocument)
        {
            XElement projectElement = projectDocument.Element(ns + "Project");
            if (null == projectElement)
            {
                throw new InvalidDataException("Project file '" + projectFileName + "' is corrupt. Couldn't find Project XML element.");
            }

            var compileItemGroupElement = from itemGroupElement in projectElement.Elements(ns + "ItemGroup")
                                          where itemGroupElement.Elements(ns + "ClCompile").Count() > 0
                                          select itemGroupElement;
            XElement result = compileItemGroupElement.SingleOrDefault();
            if (null == result)
            {
                throw new InvalidDataException("Project file '" + projectFileName + "' is corrupt. Couldn't find ItemGroup XML element with the source files to compile.");
            }
            return result;
        }

        private static XElement AddExcludedFromBuild(XNamespace ns, XElement element)
        {
            XName excludedName = ns + "ExcludedFromBuild";
            XElement existingExlude = element.Element(excludedName);
            if (existingExlude != null)
            {
                existingExlude.Remove();
            }
            element.Add(new XElement(excludedName, "true"));
            return element;
        }

        private void ReadWriteFilters(string projectFileName, UnityFileMerger merger)
        {
            string projectFiltersFileName = projectFileName + ".filters";
            if (!File.Exists(projectFiltersFileName))
                return;

            XDocument projectFiltersDocument = XDocument.Load(projectFiltersFileName);
            if (null == projectFiltersDocument)
            {
                throw new InvalidDataException("Couldn't load project filters file '" + projectFiltersFileName + "'.");
            }

            XNamespace ns = projectFiltersDocument.Root.Name.Namespace;
            XElement compileItemGroupElement = GetCompileItemGroupElement(projectFileName, ns, projectFiltersDocument);

            compileItemGroupElement.Add(
                from unityFileName in merger.UnityFilePaths
                select new XElement(ns + "ClCompile", new XAttribute("Include", unityFileName)));

            string destProjectFiltersFileName = _settings.ModifyFileName(projectFiltersFileName);
            ModifiedTextFileWriter writer = new ModifiedTextFileWriter(destProjectFiltersFileName, _settings.BuildOptions.ShouldForceWriteCachedFiles());
            if (writer.Write(projectFiltersDocument.ToString()))
            {
                _settings.Output.WriteLine("Creating project filters file " + destProjectFiltersFileName);
            }
        }

        public override void ReadWrite(string projectFileName, SolutionInfo solutionInfo, XDocument projectDocument, XNamespace ns)
        {
            XElement compileItemGroupElement = GetCompileItemGroupElement(projectFileName, ns, projectDocument);

            var cppFileNameElements = from compileElement in compileItemGroupElement.Elements(ns + "ClCompile")
                                      where !compileElement.HasElements        // Exclude any files that have special handling, such as excluded from build, precompiled headers, etc.
                                      select compileElement;
            var cppFileNames = from compileElement in cppFileNameElements
                               select compileElement.Attribute("Include").Value;

            string precompiledHeaderFileName = GetPrecompiledHeader(projectDocument, ns);
            ProjectInfo projectInfo = new ProjectInfo(solutionInfo, projectFileName, cppFileNames.ToList(), precompiledHeaderFileName);

            UnityFileMerger merger = new UnityFileMerger(_settings);
            merger.Process(projectInfo);

            foreach (XElement cppFileNameElement in cppFileNameElements)
            {
                AddExcludedFromBuild(ns, cppFileNameElement);
            }

            compileItemGroupElement.Add(
                from unityFileName in merger.UnityFilePaths
                select new XElement(ns + "ClCompile", new XAttribute("Include", unityFileName)));

            if (_settings.BuildOptions.DisablePrecompiledHeaders)
            {
                DisablePrecompiledHeaders(projectDocument, ns);
            }

            ReadWriteFilters(projectFileName, merger);
        }
    }

    internal class SingleProjectReaderWriterPreVS2010 : SingleProjectReaderWriterBase
    {
        public SingleProjectReaderWriterPreVS2010(Settings settings)
            : base(settings)
        {
        }

        private static bool IsValidCppFileName(string fileName)
        {
            string extension = Path.GetExtension(fileName);
            return extension == ".cpp" || extension == ".cxx" || extension == ".c" || extension == ".cc";
        }

        private XElement GetConfigurationElement(XDocument projectDocument, XNamespace ns)
        {
            var configElements =
                from configElement in projectDocument.Descendants(ns + "Configuration")
                where configElement.Attribute(ns + "Name") != null && configElement.Attribute(ns + "Name").Value == _settings.BuildOptions.Config
                select configElement;
            return configElements.SingleOrDefault();
        }

        private string GetPrecompiledHeader(XDocument projectDocument, XNamespace ns)
        {
            XElement configElement = GetConfigurationElement(projectDocument, ns);
            if (null == configElement)
                return string.Empty;

            XElement precompiledHeaderElement =
                (from toolElement in configElement.Elements(ns + "Tool")
                where toolElement.Attribute(ns + "UsePrecompiledHeader") != null && toolElement.Attribute(ns + "UsePrecompiledHeader").Value == "2"
                select toolElement).SingleOrDefault();
            if (null == precompiledHeaderElement)
                return string.Empty;

            XAttribute precompiledHeader = precompiledHeaderElement.Attribute(ns + "PrecompiledHeaderThrough");
            if (null != precompiledHeader)
                return precompiledHeader.Value;
            else
                return "StdAfx.h";
        }

        private void DisablePrecompiledHeaders(XDocument projectDocument, XNamespace ns)
        {
            var pchAttributes =
                from pchAttribute in projectDocument.Descendants().Attributes(ns + "UsePrecompiledHeader")
                select pchAttribute;
            foreach (XAttribute pchAttribute in pchAttributes.ToList())
            {
                pchAttribute.Value = "0";
            }
        }

        public override void ReadWrite(string projectFileName, SolutionInfo solutionInfo, XDocument projectDocument, XNamespace ns)
        {
            var cppFileNameElements =
                from cppFileElement in projectDocument.Descendants(ns + "File")
                where IsValidCppFileName(cppFileElement.Attribute("RelativePath").Value) && !cppFileElement.HasElements     // Exclude any files that have special handling, such as excluded from build, precompiled headers, etc.
                select cppFileElement;
            var cppFileNames =
                from cppFileElement in cppFileNameElements
                select cppFileElement.Attribute("RelativePath").Value;

            string precompiledHeaderFileName = GetPrecompiledHeader(projectDocument, ns);
            ProjectInfo projectInfo = new ProjectInfo(solutionInfo, projectFileName, cppFileNames.ToList(), precompiledHeaderFileName);

            UnityFileMerger merger = new UnityFileMerger(_settings);
            merger.Process(projectInfo);

            cppFileNameElements.Remove();

            XElement filesElement = projectDocument.Descendants(ns + "Files").Single();
            filesElement.Add(
                from unityFileName in merger.UnityFilePaths
                select new XElement(ns + "File", new XAttribute("RelativePath", unityFileName)));

            if (_settings.BuildOptions.DisablePrecompiledHeaders)
            {
                DisablePrecompiledHeaders(projectDocument, ns);
            }
        }
    }

    public class ProjectReaderWriter
    {
        private Settings _settings;

        public ProjectReaderWriter(Settings settings)
        {
            _settings = settings;
        }

        public void ReadWrite(SolutionInfo solutionInfo)
        {
            SingleProjectReaderWriterBase singleProjectReaderWriter = null;
            if (solutionInfo.Version == VisualStudioVersion.VS2010)
                singleProjectReaderWriter = new SingleProjectReaderWriterVS2010(_settings);
            else
                singleProjectReaderWriter = new SingleProjectReaderWriterPreVS2010(_settings);
            
            foreach (string projectFileName in solutionInfo.ProjectFileNames)
            {
                ReadWrite(projectFileName, solutionInfo, singleProjectReaderWriter);
            }
        }

        private void ReadWrite(string projectFileName, SolutionInfo solutionInfo, SingleProjectReaderWriterBase singleProjectReaderWriter)
        {
            XDocument projectDocument = XDocument.Load(projectFileName);
            if (null == projectDocument)
            {
                throw new InvalidDataException("Couldn't load project file '" + projectFileName + "'.");
            }

            XNamespace ns = projectDocument.Root.Name.Namespace;
            singleProjectReaderWriter.ReadWrite(projectFileName, solutionInfo, projectDocument, ns);

            string destProjectFileName = _settings.ModifyFileName(projectFileName);
            ModifiedTextFileWriter writer = new ModifiedTextFileWriter(destProjectFileName, _settings.BuildOptions.ShouldForceWriteCachedFiles());
            if (writer.Write(projectDocument.ToString()))
            {
                _settings.Output.WriteLine("Creating project file " + destProjectFileName);
            }
        }
    }
}
