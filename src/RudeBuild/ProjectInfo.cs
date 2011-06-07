﻿using System.Collections.Generic;
using System.IO;

namespace RudeBuild
{
    public class ProjectInfo
    {
        private SolutionInfo _solution;
        public SolutionInfo Solution
        {
            get { return _solution; }
        }

        private string _fileName;
        public string FileName
        {
            get { return _fileName; }
        }

        private string _name;
        public string Name
        {
            get { return _name; }
        }

        private IList<string> _cppFileNames;
        public IList<string> CppFileNames
        {
            get { return _cppFileNames; }
        }

        public ProjectInfo(SolutionInfo solution, string fileName, IList<string> cppFileNames)
        {
            _solution = solution;
            _fileName = fileName;
            _cppFileNames = cppFileNames;
            _name = Path.GetFileNameWithoutExtension(fileName);
        }
    }
}