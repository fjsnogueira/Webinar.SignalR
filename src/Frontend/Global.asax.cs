﻿namespace Frontend
{
    using System.Web;
    using System.Web.Mvc;
    using System.Web.Optimization;
    using System.Web.Routing;
    using Autofac;
    using Autofac.Integration.SignalR;
    using Microsoft.AspNet.SignalR;
    using NServiceBus;
    using Shared;

    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            ConfigureAndStartTheBus();

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        void ConfigureAndStartTheBus()
        {
            RabbitMqConnectionString.EnsureConnectionStringIsProvided();

            var builder = new ContainerBuilder();

            builder.RegisterHubs(typeof(MvcApplication).Assembly);

            var container = builder.Build();

            var configuration = new EndpointConfiguration("Frontend");
            configuration.UseContainer<AutofacBuilder>(c => c.ExistingLifetimeScope(container));
            configuration.UseTransport<RabbitMQTransport>()
                .ConnectionString(RabbitMqConnectionString.Value);
            configuration.UsePersistence<InMemoryPersistence>();
            configuration.EnableInstallers();

            endpoint = Endpoint.Start(configuration).GetAwaiter().GetResult();

            var updater = new ContainerBuilder();
            updater.RegisterInstance(endpoint).As<IMessageSession>().ExternallyOwned();
            updater.Update(container);

            GlobalHost.DependencyResolver = new AutofacDependencyResolver(container);
        }

        protected void Application_End()
        {
            endpoint.Stop().GetAwaiter().GetResult();
        }

        IEndpointInstance endpoint;
    }
}