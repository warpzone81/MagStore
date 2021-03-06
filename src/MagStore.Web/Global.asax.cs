﻿using System.Reflection;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using MagStore.Azure;
using MagStore.Indexes;
using MagStore.Infrastructure;
using MagStore.Infrastructure.Interfaces;
using MagStore.Provider;
using MagStore.Web.Infrastructure;
using MagStore.Web.Models.Product;
using Microsoft.Practices.ServiceLocation;
using Raven.Client;
using Raven.Client.Document;
using Raven.Client.Indexes;
using SagePayMvc;
using Component = Castle.MicroKernel.Registration.Component;

namespace MagStore.Web
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class MvcApplication : System.Web.HttpApplication
    {
        public static IWindsorContainer Container;

        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }


        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.IgnoreRoute("{*favicon}", new { favicon = @"(.*/)?favicon.ico(/.*)?" });

            routes.MapRoute(
                "Default", // Route name
                "{controller}/{action}/{id}", // URL with parameters
                new { controller = "Home", action = "Index", id = UrlParameter.Optional } // Parameter defaults
            );

        }

        protected void Application_Start()
        {
            Container = new WindsorContainer();

            // Common Service Locator
            ServiceLocator.SetLocatorProvider(() => new WindsorServiceLocator(Container));

            // RavenDB embedded
            Container.Register(Component.For<IDocumentStore>().UsingFactoryMethod(GetDocumentStore).LifeStyle.Singleton);
            Container.Register(Component.For<IDocumentSession>().UsingFactoryMethod(x => x.Resolve<IDocumentStore>().OpenSession()).LifestylePerWebRequest().OnDestroy(x => x.SaveChanges()));
            Container.Register(Component.For<RoleProvider>().ImplementedBy<RavenDbRoleProvider>().LifeStyle.Singleton);
            Container.Register(Component.For<IRepository>().ImplementedBy<RavenRepository>().LifestylePerWebRequest());
            Container.Register(Component.For<IShop>().ImplementedBy<Shop>().LifeStyle.Singleton);
            Container.Register(Component.For<IStorageAccessor>().UsingFactoryMethod(GetStorageAccessor).LifeStyle.PerWebRequest);
            Container.Register(Component.For<ITransactionRegistrar>().ImplementedBy<TransactionRegistrar>().LifestylePerWebRequest());
            ControllerBuilder.Current.SetControllerFactory(new WindsorControllerFactory(Container));
            Container.Register(Classes.FromAssembly(Assembly.GetExecutingAssembly()).BasedOn<IController>().LifestyleTransient());

            AreaRegistration.RegisterAllAreas();
            RegisterGlobalFilters(GlobalFilters.Filters);
            RegisterRoutes(RouteTable.Routes);
            ModelBinders.Binders.Add(typeof(EditProductInputModel), new TagPropertyModelBinder());
            ModelBinders.Binders.Add(typeof(CreateProductInputModel), new TagPropertyModelBinder());
        }

        protected void Session_End()
        {
            Container.Resolve<IFormsAuthenticationService>().SignOut();
        }

        protected void Application_End()
        {
            var documentStore = Container.Resolve<IDocumentStore>();
            documentStore.Dispose();
            Container.Dispose();
        }

        private IDocumentStore GetDocumentStore()
        {
            var documentStore = new DocumentStore
            {
                ApiKey = "b23f5154-30b0-48f8-b619-76ee77d0234d",
                Url = "https://ec2-eu4.cloudbird.net/databases/c818ddc6-dc4b-4b57-a439-4329fff0e61b.rdbtest-mag"
            };
            documentStore.Initialize();
            IndexCreation.CreateIndexes(typeof(Products_FullText).Assembly, documentStore);
            return documentStore;
        }

        private IStorageAccessor GetStorageAccessor()
        {
            var storageAccessor = new StorageAccessor
            (
                "magshopstrg",
                "H3g2iG5XyUzX5BhUqBtw5VRtdSN++0aNhXDhKHpEJe2kDh/oSEOGbrhKDQ0AkdVdM0P+Ons+7mH2FMNzxNyddw=="
            );
            return storageAccessor;
        }
    }
}