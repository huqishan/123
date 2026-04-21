using System;
using System.Collections.Generic;
//using System.Configuration.Install;
using System.Linq;
using System.Net.Mime;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Infrastructure.WindowsServer
{
    public static class WindowsServerManage
    {
        //public static bool Install(string serviceName)
        //{
        //    if (ServiceController.GetServices().Where(r => r.ServiceName == serviceName).Count() == 0)
        //    {
        //        AssemblyInstaller assemblyInstaller = new AssemblyInstaller();
        //        assemblyInstaller.UseNewContext = true;
        //        assemblyInstaller.Path = $"{AppDomain.CurrentDomain.BaseDirectory}WindowsService.exe";
        //        assemblyInstaller.Install(null);
        //        assemblyInstaller.Commit(null);
        //        assemblyInstaller.Dispose();
        //    }
        //    return true;
        //}
        //public static bool Start(string serviceName)
        //{
        //    ServiceController service = new ServiceController(serviceName);
        //    if (service.Status != ServiceControllerStatus.Running)
        //    {
        //        service.Start();
        //        service.WaitForStatus(ServiceControllerStatus.Running);
        //    }
        //    return true;
        //}
        //public static bool Stop(string serviceName)
        //{
        //    ServiceController service = new ServiceController(serviceName);
        //    if (service.Status != ServiceControllerStatus.Stopped)
        //    {
        //        service.Stop();
        //        service.WaitForStatus(ServiceControllerStatus.Stopped);
        //    }
        //    return true;
        //}
        //public static bool Unload(string serviceName)
        //{
        //    ServiceController service = new ServiceController(serviceName);
        //    if (service.Status == ServiceControllerStatus.Stopped)
        //    {
        //        System.Diagnostics.Process.Start("cmd.exe", $"/C sc delete \"{serviceName}\"").Dispose();
        //        return true;
        //    }
        //    return false;
        //}
    }
}
