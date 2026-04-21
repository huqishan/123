using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Infrastructure.CSharp
{
    public static class CSharpManage
    {
        public static List<string> ErrorMessage = new List<string>();
        public static bool Compile(string source, string outputfile)
        {
            string[] references = Directory.GetFiles("./").Where(r => System.IO.Path.GetExtension(r).Equals(".dll") && !Path.GetFileName(r).Contains("HPSocket4C") && !Path.GetFileName(r).Contains("lua")).ToArray();
            CompilerParameters compilerParam = new CompilerParameters(references, outputfile, true);
            //获取当前进程
            Process currentProcess = Process.GetCurrentProcess();
            //获取进程的主模块名称，即exe名称
            string exeName = currentProcess.ProcessName + ".exe";
            compilerParam.ReferencedAssemblies.Add(exeName);
            compilerParam.TreatWarningsAsErrors = false;
            compilerParam.GenerateInMemory = false;
            compilerParam.IncludeDebugInformation = true;
            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerResults results = provider.CompileAssemblyFromSource(compilerParam, new string[] { source });
            ErrorMessage?.Clear();
            if (!results.Errors.HasErrors)
            {
                Type t = results.CompiledAssembly.GetType("MyClass");
                if (t != null)
                {
                    object o = results.CompiledAssembly.CreateInstance("MyClass");
                }
                return true;
            }
            foreach (CompilerError error in results.Errors)
            {
                if (error.IsWarning) continue;
                ErrorMessage.Add($"{error.ErrorNumber} {error.ErrorText} {error.Line} {error.Column}");
            }
            return false;
        }
    }
}
