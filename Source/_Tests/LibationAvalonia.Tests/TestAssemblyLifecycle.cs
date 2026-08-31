using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace LibationAvalonia.Tests;

[TestClass]
public class TestAssemblyLifecycle
{
	[AssemblyCleanup]
	public static Task Cleanup() => HeadlessTestHost.DisposeAsync();
}
