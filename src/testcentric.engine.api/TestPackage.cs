﻿// ***********************************************************************
// Copyright (c) Charlie Poole and TestCentric contributors.
// Licensed under the MIT License. See LICENSE file in root directory.
// ***********************************************************************

using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace TestCentric.Engine
{
    /// <summary>
    /// TestPackage holds information about a set of test files to
    /// be loaded by a TestRunner. Each TestPackage represents
    /// tests for one or more test files. TestPackages may be named
    /// or anonymous, depending on the constructor used.
    /// 
    /// Upon construction, a package is given an ID (string), which
    /// remains unchanged for the lifetime of the TestPackage instance.
    /// The package ID is passed to the test framework for use in generating
    /// test IDs.
    /// 
    /// A runner that reloads test assemblies and wants the ids to remain stable
    /// should avoid creating a new package but should instead use the original
    /// package, changing settings as needed. This gives the best chance for the
    /// tests in the reloaded assembly to match those originally loaded.
    /// </summary>
    public class TestPackage : IXmlSerializable
    {
        #region Construction and Initialization

        /// <summary>
        /// Construct a top-level TestPackage that wraps one or more
        /// test files, contained as subpackages.
        /// </summary>
        /// <param name="testFiles">Names of all the test files</param>
        /// <remarks>
        /// Semantically equivalent to the IList constructor.
        /// </remarks>
        public TestPackage(params string[] testFiles)
        {
            InitializeSubPackages(testFiles);
        }

        /// <summary>
        /// Construct a top-level TestPackage that wraps one or more
        /// test files, contained as subpackages.
        /// </summary>
        /// <param name="testFiles">Names of all the test files.</param>
        /// <remarks>
        /// Semantically equivalent to the array constructor.
        /// </remarks>
        public TestPackage(IList<string> testFiles)
        {
            InitializeSubPackages(testFiles);
        }

        /// <summary>
        /// Default Constructor is required for XmlSerializer
        /// </summary>
        public TestPackage() { }

        private void InitializeSubPackages(IList<string> testFiles)
        {
            foreach (string testFile in testFiles)
                AddSubPackage(testFile);
        }

        private static int _nextID = 0;

        private static string GetNextID()
        {
            return (_nextID++).ToString();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Every test package gets a unique ID used to prefix test IDs within that package.
        /// </summary>
        /// <remarks>
        /// The generated ID is only unique for packages created within the same application domain.
        /// For that reason, NUnit pre-creates all test packages that will be needed.
        /// </remarks>
        public string ID { get; private set; } = GetNextID();

        /// <summary>
        /// Gets the name of the package
        /// </summary>
        public string Name
        {
            get { return FullName == null ? null : Path.GetFileName(FullName); }
        }

        /// <summary>
        /// Gets the path to the file containing tests. It may be
        /// an assembly or a recognized project type.
        /// </summary>
        public string FullName { get; private set; }

        public bool IsAssemblyPackage {
            get
            {
                if (FullName == null)
                    return false;

                var ext = Path.GetExtension(FullName).ToLowerInvariant();
                return ext == ".dll" || ext == ".exe";
            }
        }

        /// <summary>
        /// Gets the list of SubPackages contained in this package
        /// </summary>
        public IList<TestPackage> SubPackages { get; } = new List<TestPackage>();

        /// <summary>
        /// Returns true if this package has any SubPackages.
        /// </summary>
        public bool HasSubPackages => SubPackages.Count > 0;

        /// <summary>
        /// Gets the settings dictionary for this package.
        /// </summary>
        public IDictionary<string, object> Settings { get; } = new Dictionary<string, object>();

        #endregion

        #region Methods

        /// <summary>
        /// Add a subpackage to the package and apply all the settings
        /// from the current package to the subpackage.
        /// </summary>
        /// <param name="subPackage">The subpackage to be added</param>
        public void AddSubPackage(TestPackage subPackage)
        {
            SubPackages.Add(subPackage);

            foreach (var key in Settings.Keys)
                subPackage.Settings[key] = Settings[key];
        }

        /// <summary>
        /// Add a subpackage to the package, specifying its name. This is
        /// the only way to add a named subpackage to the top-level package.
        /// </summary>
        /// <param name="packageName">The name of the subpackage to be added</param>
        public TestPackage AddSubPackage(string packageName)
        {
            var subPackage = new TestPackage() { FullName = Path.GetFullPath(packageName) };
            SubPackages.Add(subPackage);

            return subPackage;
        }

        /// <summary>
        /// Add a setting to a package and all of its subpackages.
        /// </summary>
        /// <param name="name">The name of the setting</param>
        /// <param name="value">The value of the setting</param>
        /// <remarks>
        /// Once a package is created, subpackages may have been created
        /// as well. If you add a setting directly to the Settings dictionary
        /// of the package, the subpackages are not updated. This method is
        /// used when the settings are intended to be reflected to all the
        /// subpackages under the package.
        /// </remarks>
        public void AddSetting(string name, object value)
        {
            Settings[name] = value;
            foreach (var subPackage in SubPackages)
                subPackage.AddSetting(name, value);
        }

        /// <summary>
        /// Return the value of a setting or a default.
        /// </summary>
        /// <param name="name">The name of the setting</param>
        /// <param name="defaultSetting">The default value</param>
        /// <returns></returns>
        public T GetSetting<T>(string name, T defaultSetting)
        {
            return Settings.ContainsKey(name)
                ? (T)Settings[name]
                : defaultSetting;
        }

        public delegate bool SelectorDelegate(TestPackage p);

        /// <summary>
        /// Select individual packages from the tree of packages
        /// based on a selection criteria.
        /// </summary>
        public IList<TestPackage> Select(SelectorDelegate selector)
        {
            var selection = new List<TestPackage>();

            AccumulatePackages(this, selection, selector);

            return selection;
        }

        private static void AccumulatePackages(TestPackage package, IList<TestPackage> selection, SelectorDelegate selector)
        {
            if (selector(package))
                selection.Add(package);

            foreach (var subPackage in package.SubPackages)
                AccumulatePackages(subPackage, selection, selector);
        }

        public XmlSchema GetSchema() => null;

        public void ReadXml(XmlReader reader)
        {
            // We start out positioned on the root element which
            // has already been read for us, so process its attributes.
            ID = reader.GetAttribute("id");
            FullName = reader.GetAttribute("fullname");

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        switch (reader.Name)
                        {
                            case "Settings":
                                while (reader.MoveToNextAttribute())
                                    AddSetting(reader.Name, reader.Value);
                                reader.MoveToElement();
                                break;

                            case "TestPackage":
                                ReadSubPackage();
                                break;

                            default:
                                break;
                        }
                        break;

                    case XmlNodeType.EndElement:
                        switch (reader.Name)
                        {
                            case "TestPackage":
                                return;
                        }
                        break;

                    default:
                        break;
                }
            }

            void ReadSubPackage()
            {
                var subPackage = new TestPackage();
                SubPackages.Add(subPackage);
                subPackage.ReadXml(reader);
            }
        }

        public void WriteXml(XmlWriter xmlWriter)
        {
            //writer.WriteStartElement("TestPackage");

            WritePackageAttributes();
            WriteSettings();
            WriteSubPackages();

            //xmlWriter.WriteEndElement();

            void WritePackageAttributes()
            {
                xmlWriter.WriteAttributeString("id", ID);

                if (FullName != null)
                    xmlWriter.WriteAttributeString("fullname", FullName);
            }

            void WriteSettings()
            {
                if (Settings.Count != 0)
                {
                    xmlWriter.WriteStartElement("Settings");

                    foreach (var pair in Settings)
                        xmlWriter.WriteAttributeString(pair.Key, pair.Value.ToString());

                    xmlWriter.WriteEndElement();
                }
            }

            void WriteSubPackages()
            {
                foreach (var subPackage in SubPackages)
                {
                    xmlWriter.WriteStartElement("TestPackage");
                    subPackage.WriteXml(xmlWriter);
                    xmlWriter.WriteEndElement();
                }
            }
        }

        #endregion
    }
}
