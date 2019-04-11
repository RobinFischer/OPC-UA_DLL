using System.Collections;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

namespace OpcClientHda
{
    public class Client
    {
        public async void Start(string endpoint)
        {
            Console.WriteLine("Step 1 - Create a config.");
            var config = new ApplicationConfiguration()
            {
                ApplicationName = "test-opc",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration { ApplicationCertificate = new CertificateIdentifier() },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
            };
            await config.Validate(ApplicationType.Client);
            if (true) //config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                config.CertificateValidator.CertificateValidation += (s, e) => { e.Accept = (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted); };
            }

            Console.WriteLine("Step 2 - Create a session with your server.");
            using (var session = await Session.Create(config, new ConfiguredEndpoint(null, new EndpointDescription(endpoint)), true, "", 60000, null, null))
            {
                Console.WriteLine("Step 3 - Browse the server namespace.");
                ReferenceDescriptionCollection refs;
                byte[] cp;
                session.Browse(null, null, ObjectIds.ObjectsFolder, 0u, BrowseDirection.Forward, ReferenceTypeIds.HierarchicalReferences, true, (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method, out cp, out refs);
                Console.WriteLine("DisplayName: BrowseName, NodeClass");
                foreach (var rd in refs)
                {
                    Console.WriteLine(rd.DisplayName + ": " + rd.BrowseName + ", " + rd.NodeClass);
                    ReferenceDescriptionCollection nextRefs;
                    byte[] nextCp;
                    session.Browse(null, null, ExpandedNodeId.ToNodeId(rd.NodeId, session.NamespaceUris), 0u, BrowseDirection.Forward, ReferenceTypeIds.HierarchicalReferences, true, (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method, out nextCp, out nextRefs);
                    foreach (var nextRd in nextRefs)
                    {
                        Console.WriteLine("+ " + nextRd.DisplayName + ": " + nextRd.BrowseName + ", " + nextRd.NodeClass);
                    }
                }

                Console.WriteLine("Step 4 - Create a subscription. Set a faster publishing interval if you wish.");
                var subscription = new Subscription(session.DefaultSubscription) { PublishingInterval = 1000 };

                Console.WriteLine("Step 5 - Add a list of items you wish to monitor to the subscription.");
                var list = new List<MonitoredItem> {
                new MonitoredItem(subscription.DefaultItem) { DisplayName = "aaatime", StartNodeId = "i=10004" } };
                list.ForEach(i => i.Notification += OnNotification);
                subscription.AddItems(list);

                Console.WriteLine("Step 6 - Add the subscription to the session.");
                session.AddSubscription(subscription);
                subscription.Create();

                Console.WriteLine("Finished client initialization");
            }
        }

        private static void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            foreach (var value in item.DequeueValues())
            {
                Console.WriteLine("{0}: {1}, {2}, {3}", item.DisplayName, value.Value, value.SourceTimestamp, value.StatusCode);
            }
        }
        //public string debug = "";
        //// Start is called before the first frame update

        //public async void Start(string endpoint)
        //{
        //    Debug.WriteLine("Step 1 - Create a config.");
        //    debug = "Step 1 - Create a config.";
        //    var config = new ApplicationConfiguration()
        //    {
        //        ApplicationName = "test-opc",
        //        ApplicationType = ApplicationType.Client,
        //        SecurityConfiguration = new SecurityConfiguration { ApplicationCertificate = new CertificateIdentifier() },
        //        TransportConfigurations = new TransportConfigurationCollection(),
        //        TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
        //        ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
        //    };
        //    await config.Validate(ApplicationType.Client);
        //    if (true)//config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
        //    {
        //        config.CertificateValidator.CertificateValidation += (s, e) => { e.Accept = (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted); };
        //    }

        //    Debug.WriteLine("Step 2 - Create a session with your server.");
        //    debug = "Step 2 - Create a session with your server.";
        //    using (var session = await Session.Create(config, new ConfiguredEndpoint(null, new EndpointDescription(endpoint)), true, "", 60000, null, null))
        //    {
        //        Debug.WriteLine("Step 3 - Browse the server namespace.");
        //        debug = "Step 3 - Browse the server namespace.";
        //        ReferenceDescriptionCollection refs;
        //        byte[] cp;
        //        session.Browse(null, null, ObjectIds.ObjectsFolder, 0u, BrowseDirection.Forward, ReferenceTypeIds.HierarchicalReferences, true, (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method, out cp, out refs);
        //        Debug.WriteLine("DisplayName: BrowseName, NodeClass");
        //        debug = "DisplayName: BrowseName, NodeClass";
        //        foreach (var rd in refs)
        //        {
        //            Debug.WriteLine(rd.DisplayName + ": " + rd.BrowseName + ", " + rd.NodeClass);
        //            ReferenceDescriptionCollection nextRefs;
        //            byte[] nextCp;
        //            session.Browse(null, null, ExpandedNodeId.ToNodeId(rd.NodeId, session.NamespaceUris), 0u, BrowseDirection.Forward, ReferenceTypeIds.HierarchicalReferences, true, (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method, out nextCp, out nextRefs);
        //            foreach (var nextRd in nextRefs)
        //            {
        //                Debug.WriteLine("+ " + nextRd.DisplayName + ": " + nextRd.BrowseName + ", " + nextRd.NodeClass);
        //            }
        //        }

        //        Debug.WriteLine("Step 4 - Create a subscription. Set a faster publishing interval if you wish.");
        //        debug = "Step 4 - Create a subscription. Set a faster publishing interval if you wish.";
        //        var subscription = new Subscription(session.DefaultSubscription) { PublishingInterval = 1000 };


        //        Debug.WriteLine("Step 5 - Add a list of items you wish to monitor to the subscription.");
        //        debug = "Step 5 - Add a list of items you wish to monitor to the subscription.";
        //        var list = new List<MonitoredItem> {
        //        new MonitoredItem(subscription.DefaultItem) { DisplayName = "aaatime", StartNodeId = "i=10004" } };
        //        list.ForEach(i => i.Notification += OnNotification);
        //        subscription.AddItems(list);

        //        Debug.WriteLine("Step 6 - Add the subscription to the session.");
        //        debug = "Step 6 - Add the subscription to the session.";
        //        session.AddSubscription(subscription);
        //        subscription.Create();

        //        Debug.WriteLine("Finished client initialization");
        //        debug = "Finished client initialization";
        //    }
        //}

        //private static void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        //{
        //    Debug.WriteLine("OnNoti");
        //    foreach (var value in item.DequeueValues())
        //    {
        //        Debug.WriteLine("'sup");
        //        Debug.WriteLine("{0}: {1}, {2}, {3}" + item.DisplayName + value.Value + value.SourceTimestamp + value.StatusCode);
        //    }
        //}
    }

}
