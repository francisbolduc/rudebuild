using System;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Xml.Serialization;

namespace RudeBuild
{
    public class SolutionSettings
    {
        public const string ConfigFileNameSuffix = ".RudeBuild.config";

        [DefaultValue(false)]
        public bool DisablePrecompiledHeaders { get; set; }

        public SerializableDictionary<string, List<string>> ProjectNameToExcludedCppFileNameMap { get; set; }

        public SolutionSettings()
        {
            this.SetToDefaults();
        }

        public static string GetConfigFilePath(Settings settings, SolutionInfo solutionInfo)
        {
            string solutionFileName = Path.GetFileNameWithoutExtension(solutionInfo.FilePath);
            string configFilePath = Path.Combine(Path.GetDirectoryName(solutionInfo.FilePath), solutionFileName + ConfigFileNameSuffix);
            return configFilePath;
        }

        private bool RemoveNoLongerExistingProjects(SolutionInfo solutionInfo)
        {
            bool changed = false;
            IList<string> projectNames = new List<string>(ProjectNameToExcludedCppFileNameMap.Keys);
            foreach (string projectName in projectNames)
            {
                // Check if the projects we have stored settings for still exists. If they don't, remove them.
                if (!solutionInfo.ProjectNames.Contains(projectName))
                {
                    ProjectNameToExcludedCppFileNameMap.Remove(projectName);
                    changed = true;
                }
            }

            return changed;
        }

        private bool AddNewProjects(SolutionInfo solutionInfo)
        {
            bool changed = false;
            foreach (string projectName in solutionInfo.ProjectNames)
            {
                if (!ProjectNameToExcludedCppFileNameMap.ContainsKey(projectName))
                {
                    ProjectNameToExcludedCppFileNameMap.Add(projectName, new List<string>());
                    changed = true;
                }
            }
            return changed;
        }

        private bool RemoveNoLongerExistingCppFileNames(ProjectInfo projectInfo)
        {
            // Check if all the file names we have stored settings for still exist. If they don't, remove them.
            bool changed = false;
            List<string> cppFileNames = null;
            ProjectNameToExcludedCppFileNameMap.TryGetValue(projectInfo.Name, out cppFileNames);
            if (null != cppFileNames)
            {
                for (int i = 0; i < cppFileNames.Count; ++i)
                {
                    if (!projectInfo.CppFileNames.Contains(cppFileNames[i]))
                    {
                        cppFileNames.RemoveAt(i);
                        --i;
                        changed = true;
                    }
                }
            }
            return changed;
        }

        public bool Update(SolutionInfo solutionInfo)
        {
            if (null == ProjectNameToExcludedCppFileNameMap)
            {
                ProjectNameToExcludedCppFileNameMap = new SerializableDictionary<string, List<string>>();
            }

            bool changed = RemoveNoLongerExistingProjects(solutionInfo);
            changed = AddNewProjects(solutionInfo) || changed;

            foreach (string projectName in solutionInfo.ProjectNames)
            {
                ProjectInfo projectInfo = null;
                solutionInfo.Projects.TryGetValue(projectName, out projectInfo);
                if (null == projectInfo)
                    throw new InvalidDataException("SolutionInfo does not contain ProjectInfo object for project called " + projectName);

                changed = RemoveNoLongerExistingCppFileNames(projectInfo) || changed;
            }

            return changed;
        }

        public bool UpdateAndSave(Settings settings, SolutionInfo solutionInfo)
        {
            bool changed = Update(solutionInfo);
            if (changed)
            {
                try
                {
                    Save(settings, solutionInfo);
                }
                catch (System.Exception ex)
                {
                    settings.Output.WriteLine("Couldn't save solution settings file: " + GetConfigFilePath(settings, solutionInfo));
                    settings.Output.WriteLine("because of exception: " + ex.Message);
                }
            }
            return changed;
        }

        public void ExcludeCppFileNameForProject(ProjectInfo projectInfo, string cppFileName)
        {
            if (null == ProjectNameToExcludedCppFileNameMap)
            {
                ProjectNameToExcludedCppFileNameMap = new SerializableDictionary<string, List<string>>();
            }
            if (!projectInfo.CppFileNames.Contains(cppFileName))
            {
                throw new ArgumentException("The project " + projectInfo.Name + " does not contain the file '" + cppFileName + "' and so cannot be excluded.");
            }

            List<string> cppFileNames = null;
            ProjectNameToExcludedCppFileNameMap.TryGetValue(projectInfo.Name, out cppFileNames);
            if (null == cppFileNames)
            {
                cppFileNames = new List<string>();
                cppFileNames.Add(cppFileName);
                ProjectNameToExcludedCppFileNameMap.Add(projectInfo.Name, cppFileNames);
            }
            else
            {
                if (!cppFileNames.Contains(cppFileName))
                    cppFileNames.Add(cppFileName);
            }
        }

        public bool IsExcludedCppFileNameForProject(ProjectInfo projectInfo, string cppFileName)
        {
            if (null == ProjectNameToExcludedCppFileNameMap)
                return false;
            List<string> cppFileNames = null;
            ProjectNameToExcludedCppFileNameMap.TryGetValue(projectInfo.Name, out cppFileNames);
            if (null == cppFileNames)
                return false;
            return cppFileNames.Contains(cppFileName);
        }

        public static SolutionSettings Load(Settings settings, SolutionInfo solutionInfo)
        {
            string configFilePath = GetConfigFilePath(settings, solutionInfo);
            if (File.Exists(configFilePath))
            {
                using (TextReader textReader = new StreamReader(configFilePath))
                {
                    XmlSerializer deserializer = new XmlSerializer(typeof(SolutionSettings));
                    try
                    {
                        SolutionSettings solutionSettings = (SolutionSettings)deserializer.Deserialize(textReader);
                        return solutionSettings;
                    }
                    catch
                    {
                        // ignore any errors
                    }
                }
            }

            return new SolutionSettings();
        }

        public void Save(Settings settings, SolutionInfo solutionInfo)
        {
            string configFilePath = GetConfigFilePath(settings, solutionInfo);
            using (TextWriter textWriter = new StreamWriter(configFilePath))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(SolutionSettings));
                serializer.Serialize(textWriter, this);
            }
        }
    }
}