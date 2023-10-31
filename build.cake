// Load the recipe
#load nuget:?package=TestCentric.Cake.Recipe&version=1.1.0-dev00058
// Comment out above line and uncomment below for local tests of recipe changes
//#load ../TestCentric.Cake.Recipe/recipe/*.cake

using System.Xml;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Threading.Tasks;

//////////////////////////////////////////////////////////////////////
// INITIALIZE BUILD SETTINGS
//////////////////////////////////////////////////////////////////////

BuildSettings.Initialize(
	Context,
	"TestCentric.Engine.Api",
	solutionFile: "TestCentric.Engine.Api.sln",
	githubRepository: "TestCentric.Engine.Api"
);

//////////////////////////////////////////////////////////////////////
// DEFINE PACKAGE
//////////////////////////////////////////////////////////////////////

var EngineApiPackage = new NuGetPackage(
	id: "TestCentric.Engine.Api",
	title: "TestCentric Engine Api Assembly",
	description: "This package includes the testcentric.agent.api assembly, containing the interfaces used in creating pluggable agents.",
	basePath: "bin/" + BuildSettings.Configuration,
	source: "nuget/TestCentric.Engine.Api.nuspec",
	checks: new PackageCheck[] {
		HasFiles("LICENSE.txt", "testcentric.png"),
		HasDirectory("lib/net20").WithFiles("TestCentric.Engine.Api.dll"),
		HasDirectory("lib/net462").WithFiles("TestCentric.Engine.Api.dll"),
		HasDirectory("lib/netstandard2.0").WithFiles("Testcentric.Engine.Api.dll")
	});

BuildSettings.Packages.Add(EngineApiPackage);

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("AppVeyor")
	.Description("Targets to run on AppVeyor")
	.IsDependentOn("DumpSettings")
	.IsDependentOn("Build")
	.IsDependentOn("Test")
	.IsDependentOn("Package")
	.IsDependentOn("Publish")
	.IsDependentOn("CreateDraftRelease")
	.IsDependentOn("CreateProductionRelease");

Task("Travis")
	.Description("Targets to run on Travis")
    .IsDependentOn("Build")
    .IsDependentOn("Test");

Task("Default")
    .IsDependentOn("Build");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(CommandLineOptions.Target);
