using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Windows.Storage;
using System.Security.Cryptography.X509Certificates;


namespace EndpointTesting
{
    public class Class1
    {
        public string ENDPOINT_FOUND;
        public static bool m_value = false;

        public void Connect(string url)
        {
            ENDPOINT_FOUND = "fail";
            System.Diagnostics.Debug.WriteLine(".Net Core OPC UA Console Client sample");
            string endpointURL = url;

            try
            {
                Task t = ConsoleSampleClient(endpointURL);
                t.Wait();
                ENDPOINT_FOUND = "connected";
                
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Exit due to Exception: {0}", e.Message);
                
            }
        }

        public static async Task ConsoleSampleClient(string endpointURL)
        {
            System.Diagnostics.Debug.WriteLine("1 - Create an Application Configuration.");

            ApplicationInstance application = new ApplicationInstance();
            application.ApplicationName = "UA Sample Client";
            application.ApplicationType = ApplicationType.ClientAndServer;
            application.ConfigSectionName = "Opc.Ua.SampleClient";

            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            Utils.DefaultLocalFolder = localFolder.Path;


            await application.LoadApplicationConfiguration(false);
            await application.CheckApplicationInstanceCertificate(false, 0);
            var config = application.ApplicationConfiguration;

            if (!config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                config.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);
            }

            System.Diagnostics.Debug.WriteLine("2 - Discover endpoints of {0}." + endpointURL);
            Uri endpointURI = Opc.Ua.Utils.ParseUri(endpointURL);
            var endpointCollection = DiscoverEndpoints(config, endpointURI, 0);
            var selectedEndpoint = SelectUaTcpEndpoint(endpointCollection, false);

            System.Diagnostics.Debug.WriteLine("    Selected endpoint uses: {0}" +
                selectedEndpoint.SecurityPolicyUri.Substring(selectedEndpoint.SecurityPolicyUri.LastIndexOf('#') + 1));

            System.Diagnostics.Debug.WriteLine("3 - Create a session with OPC UA server.");
            var endpointConfiguration = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(selectedEndpoint.Server, endpointConfiguration);
            endpoint.Update(selectedEndpoint);

            X509Certificate2 clientCertificate = null;

            if (endpoint.Description.SecurityPolicyUri != SecurityPolicies.None)
            {
                if (config.SecurityConfiguration.ApplicationCertificate == null)
                {
                    System.Diagnostics.Debug.WriteLine("Holo: ApplicationCertificate mustbe specified");
                }
                clientCertificate = await config.SecurityConfiguration.ApplicationCertificate.Find(true);

                if (clientCertificate == null)
                {
                    System.Diagnostics.Debug.WriteLine("Holo: Applicationcertificate cannot be found");
                }

            }

            var session = await Session.Create(config, endpoint, true, "HoloClient", 60000, new UserIdentity("Admin", "Labor0.75"), null);

            System.Diagnostics.Debug.WriteLine(session);


            System.Diagnostics.Debug.WriteLine("4 - Browse the OPC UA server namespace.");
            ReferenceDescriptionCollection references;
            Byte[] continuationPoint;

            references = session.FetchReferences(ObjectIds.ObjectsFolder);

            session.Browse(
                null,
                null,
                ObjectIds.ObjectsFolder,
                0u,
                BrowseDirection.Forward,
                ReferenceTypeIds.HierarchicalReferences,
                true,
                (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method,
                out continuationPoint,
                out references);

            System.Diagnostics.Debug.WriteLine(" DisplayName, BrowseName, NodeClass");
            foreach (var rd in references)
            {
                System.Diagnostics.Debug.WriteLine(" {0}, {1}, {2}", rd.DisplayName, rd.BrowseName, rd.NodeClass);
                ReferenceDescriptionCollection nextRefs;
                byte[] nextCp;
                session.Browse(
                    null,
                    null,
                    ExpandedNodeId.ToNodeId(rd.NodeId, session.NamespaceUris),
                    0u,
                    BrowseDirection.Forward,
                    ReferenceTypeIds.HierarchicalReferences,
                    true,
                    (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method,
                    out nextCp,
                    out nextRefs);

                foreach (var nextRd in nextRefs)
                {
                    System.Diagnostics.Debug.WriteLine("   + {0}, {1}, {2}", nextRd.DisplayName, nextRd.BrowseName, nextRd.NodeClass);
                }
            }

            System.Diagnostics.Debug.WriteLine("5 - Create a subscription with publishing interval of 1 second.");
            var subscription = new Subscription(session.DefaultSubscription) { PublishingInterval = 500 };

            System.Diagnostics.Debug.WriteLine("6 - Add a list of items (server current time and status) to the subscription.");
            var list = new List<MonitoredItem> {
                new MonitoredItem(subscription.DefaultItem)
                {
                    DisplayName = "Holo: 1Hz",  StartNodeId  = "ns=7;s=St10-Leitstand.Station10.1Hz"
                }
            };
            list.ForEach(i => i.Notification += OnNotification);
            subscription.AddItems(list);

            System.Diagnostics.Debug.WriteLine("7 - Add the subscription to the session.");
            session.AddSubscription(subscription);
            subscription.Create();

            System.Diagnostics.Debug.WriteLine("8 - Running...");


        }

        private static void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            foreach (var value in item.DequeueValues())
            {
                System.Diagnostics.Debug.WriteLine("{0}: {1}, {2}, {3}", item.DisplayName, value.Value, value.SourceTimestamp, value.StatusCode);
                if (value.Value.Equals(true))
                {
                    m_value = true;
                    
                } else
                if (value.Value.Equals(false))
                {
                    m_value = false;
                    
                }
                
            }
        }

        private static void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Accepted Certificate: {0}", e.Certificate.Subject);
            e.Accept = true;

            //e.Accept = (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted);
        }

        private static EndpointDescriptionCollection DiscoverEndpoints(ApplicationConfiguration config, Uri discoveryUrl, int timeout)
        {
            // use a short timeout.
            EndpointConfiguration configuration = EndpointConfiguration.Create(config);
            configuration.OperationTimeout = timeout;

            DiscoveryClient client = DiscoveryClient.Create(
                discoveryUrl,
                EndpointConfiguration.Create(config));

            try
            {
                //ApplicationDescriptionCollection endpoints=client.FindServers(null);
                EndpointDescriptionCollection endpoints = client.GetEndpoints(null);
                ReplaceLocalHostWithRemoteHost(endpoints, discoveryUrl);

                return endpoints;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Could not fetch endpoints from url: {0}", discoveryUrl);
                System.Diagnostics.Debug.WriteLine("Reason = {0}", e.Message);
                throw e;
            }

        }

        private static EndpointDescription SelectUaTcpEndpoint(EndpointDescriptionCollection endpointCollection, bool haveCert)
        {
            EndpointDescription bestEndpoint = null;
            foreach (EndpointDescription endpoint in endpointCollection)
            {
                if (endpoint.TransportProfileUri == Profiles.UaTcpTransport)
                {
                    if (bestEndpoint == null ||
                        haveCert && (endpoint.SecurityLevel > bestEndpoint.SecurityLevel) ||
                        !haveCert && (endpoint.SecurityLevel < bestEndpoint.SecurityLevel))
                    {
                        bestEndpoint = endpoint;
                    }
                }
            }
            System.Diagnostics.Debug.WriteLine("Holo: best endpoint " + bestEndpoint.EndpointUrl);
            return bestEndpoint;
        }

        private static void ReplaceLocalHostWithRemoteHost(EndpointDescriptionCollection endpoints, Uri discoveryUrl)
        {
            foreach (EndpointDescription endpoint in endpoints)
            {
                System.Diagnostics.Debug.WriteLine("Holo" + endpoint.EndpointUrl);
                endpoint.EndpointUrl = Utils.ReplaceLocalhost(endpoint.EndpointUrl, discoveryUrl.DnsSafeHost);
                StringCollection updatedDiscoveryUrls = new StringCollection();
                foreach (string url in endpoint.Server.DiscoveryUrls)
                {
                    updatedDiscoveryUrls.Add(Utils.ReplaceLocalhost(url, discoveryUrl.DnsSafeHost));
                }
                endpoint.Server.DiscoveryUrls = updatedDiscoveryUrls;
            }
        }
    }
}
