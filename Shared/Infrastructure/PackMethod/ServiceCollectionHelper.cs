using Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Infrastructure.PackMethod
{
    public static class ServiceCollectionHelper
    {
        static IContainer _IContainer;
        static ContainerBuilder _Builder = new ContainerBuilder();
        private static Dictionary<string, Type> _Types = new Dictionary<string, Type>();
        public static void AddSingleton<TService>() where TService : class
        {
            if (_Types.Keys.Contains(typeof(TService).Name))
                _Types[typeof(TService).Name] = typeof(TService);
            else _Types[typeof(TService).Name] = typeof(TService);
            _Builder.RegisterType<TService>();
        }
        public static void AddSingletonName<TService>() where TService : class
        {
            _Builder.RegisterType<TService>().Named<TService>(typeof(TService).Name);
        }
        public static void AddSingleton<TService, TImplementer>() where TService : class
        {
            if (_Types.Keys.Contains(typeof(TService).Name))
                _Types[typeof(TService).Name] = typeof(TService);
            else _Types[typeof(TService).Name] = typeof(TService);
            _Builder.RegisterType<TImplementer>().As<TService>();
        }
        public static void AddSingleton<TService>(TService service) where TService : class
        {
            if (_Types.Keys.Contains(typeof(TService).Name))
                _Types[typeof(TService).Name] = typeof(TService);
            else _Types[typeof(TService).Name] = typeof(TService);
            _Builder.RegisterInstance(service);
        }
        public static TService Resolve<TService>() where TService : class
        {
            return _IContainer.Resolve<TService>();
        }
        public static TService Resolve<TService>(TService type) where TService : class
        {
            return _IContainer.Resolve<TService>();
        }
        public static object ResolveName(Type type)
        {
            return _IContainer.Resolve(type);
        }
        public static void Buid()
        {
            _IContainer = _Builder.Build();
        }
        public static Type GetType(string name)
        {
            if (_Types.Keys.Contains(name))
                return _Types[name];
            else return null;
        }
    }
}
