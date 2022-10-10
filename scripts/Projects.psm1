<#
.SYNOPSIS
    Provides a list of Sarifer projects and frameworks.
.DESCRIPTION
    The Projects module exports variables whose properties specify the
    various kinds of projects in the Sarifer extension, and the frameworks
	for which they are built.
#>

# .NET Framework versions for which we build.
$Frameworks = @("net472")

$Projects = @{}
$Projects.Vsix = @(
	"Sarif.Sarifer.2022",
	"Sarif.Sarifer")
$Projects.NuGet = @("Sarif.Viewer.VisualStudio.Interop")
$Projects.Library = @(
	"Sarif.Viewer.VisualStudio.Shell.2022",
	"Sarif.Viewer.VisualStudio.Shell")
$Projects.Product = $Projects.Vsix + $Projects.NuGet
$Projects.Test = @(
	"Sarif.Sarifer.UnitTests")
$Projects.All = $Projects.Product + $Projects.Test + $Projects.Library

Export-ModuleMember -Variable Frameworks, Projects