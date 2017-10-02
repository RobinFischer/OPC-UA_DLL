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


namespace OpcUa_Hololens_client
{
    public class HoloClient
    {
        public string conexion_Error;
        public string output;

        public static string reworkCode_value;
        public static bool reworkCode_status;
        public static string reworkDone_value;
        public static bool reworkDone_status;
        public static string reworkStart_value;
        public static bool reworkStart_status;

        private ApplicationConfiguration m_configuration;
        private EndpointDescription m_endpointDescription;
        private ConfiguredEndpoint m_configuredEndpoint;
        private Session m_session;
        private Subscription m_subscription;
        private List<MonitoredItem> m_list_monitoredItems=new List<MonitoredItem>();


        public HoloClient()
        {
            try
            {
                Task t = CreateApplicationConfiguration();
                t.Wait();
            }
            catch (Exception e)
            {
                conexion_Error = e.ToString();
                System.Diagnostics.Debug.WriteLine("Holo - Exception " + conexion_Error);
            }
        }

        public bool setEndpoint(string endpointURL)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("2 - Discover endpoints of {0}." + endpointURL);
                Uri endpointURI = Opc.Ua.Utils.ParseUri(endpointURL);
                var endpointCollection = DiscoverEndpoints(m_configuration, endpointURI, 0);

                m_endpointDescription = SelectUaTcpEndpoint(endpointCollection, false);

                System.Diagnostics.Debug.WriteLine("    Selected endpoint uses: {0}" +
                    m_endpointDescription.SecurityPolicyUri.Substring(m_endpointDescription.SecurityPolicyUri.LastIndexOf('#') + 1));

                return true;
            }
            catch (Exception e)
            {
                conexion_Error = e.ToString();
                return false;
            }
        }

        public bool connect(string username, string password, string reworkCode_NodeId, string reworkDone_NodeId, string reworkSinpos_NodeId, int publishInterval)
        {
            try
            {
                Task t = createSession(username, password);
                t.Wait();
                addSubscribtions(reworkCode_NodeId, reworkDone_NodeId, reworkSinpos_NodeId, publishInterval);
                return true;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Holo: Connect Error");
                conexion_Error = e.ToString();
                return false;
            }
        }

        public bool disconnect()
        {
            try
            {
                //m_session.CloseSession(null,true);
                m_session.Close();
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }


        public bool write(string nodeId, string valueToWrite)
        {

            System.Diagnostics.Debug.WriteLine("9 - Writing Integer");
            NodeId mynodeId= NodeId.Parse(nodeId);
           
            try
            {
                Node node = m_session.NodeCache.Find(mynodeId) as Node;
                DataValue mydatavalue = m_session.ReadValue(node.NodeId);

                WriteValue value = new WriteValue()
                {
                    NodeId = mynodeId,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(Convert.ChangeType(valueToWrite, mydatavalue.Value.GetType()))),
                };

                System.Diagnostics.Debug.WriteLine("Holo write: " + node.NodeId);
                System.Diagnostics.Debug.WriteLine("Holo write: " + value.Value + "..." + mydatavalue.Value.GetType());

                WriteValueCollection mywrite = new WriteValueCollection();
                mywrite.Add(value);

                StatusCodeCollection results = null;
                DiagnosticInfoCollection diagnosticInfos = null;
                m_session.Write(null, mywrite, out results, out diagnosticInfos);

                System.Diagnostics.Debug.WriteLine("Holo write Results: " + results.Count + " .... " + results[0]);

                if (results[0] == StatusCodes.Good) { return true; }
                return false;

            }
            catch (Exception e)
            {
                conexion_Error = e.ToString();
                return false;
            }
        }

        public string read(string nodeId)
        {
            try
            {
                Node node = m_session.NodeCache.Find(nodeId) as Node;
                DataValue mydatavalue = m_session.ReadValue(node.NodeId);
                return mydatavalue.Value.ToString();
            }
            catch (Exception e)
            {
                conexion_Error = e.ToString();
                return null;
            }
        }


        private async Task createSession(string username, string password)
        {
            System.Diagnostics.Debug.WriteLine("3 - Create a session with OPC UA server.");
            var endpointConfiguration = EndpointConfiguration.Create(m_configuration);
            var endpoint = new ConfiguredEndpoint(m_endpointDescription.Server, endpointConfiguration);
            endpoint.Update(m_endpointDescription);

            X509Certificate2 clientCertificate = null;

            if (endpoint.Description.SecurityPolicyUri != SecurityPolicies.None)
            {
                if (m_configuration.SecurityConfiguration.ApplicationCertificate == null)
                {
                    System.Diagnostics.Debug.WriteLine("Holo: ApplicationCertificate mustbe specified");
                }
                clientCertificate = await m_configuration.SecurityConfiguration.ApplicationCertificate.Find(true);

                if (clientCertificate == null)
                {
                    System.Diagnostics.Debug.WriteLine("Holo: Applicationcertificate cannot be found");
                }

            }

            m_session = await Session.Create(m_configuration, endpoint, true, "HoloClient", 60000, new UserIdentity(username,password), null);
            System.Diagnostics.Debug.WriteLine(m_session);
            m_session.SessionClosing+= new EventHandler(onSessionClossing);
            m_session.KeepAlive+= new KeepAliveEventHandler(onkeepalive);
        }

        private void onSessionClossing(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Holo: Session Clossing");
            reworkCode_status = false;
            reworkDone_status = false;
            reworkStart_status = false;
        }

        private void onkeepalive(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Holo: Session Onkeepalive");

            try
            {
                NodeId myNodeId = m_list_monitoredItems.First<MonitoredItem>().StartNodeId;
                System.Diagnostics.Debug.WriteLine("Holo: " + myNodeId.ToString());

                try
                {
                    //if(m_session.)
                    m_session.ReadNode(myNodeId);
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("Holo: Error trying to read firstNode");
                    reworkCode_status = false;
                    reworkDone_status = false;
                    reworkStart_status = false;
                }
            }

            catch
            {
            }


            }

        private void addSubscribtions(string reworkCode_NodeId, string reworkDone_NodeId, string reworkSinpos_NodeId, int publishInterval)
        {
            System.Diagnostics.Debug.WriteLine("5 - Create a subscription with publishing interval of 1 second.");
            m_subscription = new Subscription(m_session.DefaultSubscription) { PublishingInterval = publishInterval };

            System.Diagnostics.Debug.WriteLine("6 - Add a list of items (server current time and status) to the subscription.");

            m_list_monitoredItems.Add(new MonitoredItem(m_subscription.DefaultItem)
                                        {
                                            DisplayName = "Holo: reworkCode",
                                            StartNodeId = reworkCode_NodeId
                                        });
            m_list_monitoredItems.Add(new MonitoredItem(m_subscription.DefaultItem)
                                        {
                                            DisplayName = "Holo: reworkDone",
                                            StartNodeId = reworkDone_NodeId
                                        });
            m_list_monitoredItems.Add(new MonitoredItem(m_subscription.DefaultItem)
                                        {
                                            DisplayName = "Holo: reworkSinpos",
                                            StartNodeId = reworkSinpos_NodeId
                                        });

            m_list_monitoredItems.ForEach(i => i.Notification += OnNotification);
       
            m_subscription.AddItems(m_list_monitoredItems);

            System.Diagnostics.Debug.WriteLine("7 - Add the subscription to the session.");
            m_session.AddSubscription(m_subscription);
            m_subscription.Create();
        }

        private async Task CreateApplicationConfiguration()
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
            m_configuration = application.ApplicationConfiguration;

            if (!m_configuration.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                m_configuration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);
            }
        }

        private static void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Accepted Certificate: {0}", e.Certificate.Subject);
            e.Accept = true;
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

        private static void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            foreach (var value in item.DequeueValues())
            {
                System.Diagnostics.Debug.WriteLine("{0}: {1}, {2}, {3}", item.DisplayName, value.Value, value.SourceTimestamp, value.StatusCode);
                
                switch (item.DisplayName)
                {
                    case "Holo: reworkCode":
                        reworkCode_value= value.Value.ToString();
                        reworkCode_status = value.StatusCode.Equals(StatusCodes.Good);
                        break;
                    case "Holo: reworkDone":
                        reworkDone_value = value.Value.ToString();
                        reworkDone_status = value.StatusCode.Equals(StatusCodes.Good);
                        break;
                    case "Holo: reworkSinpos":
                        reworkStart_value = value.Value.ToString();
                        reworkStart_status = value.StatusCode.Equals(StatusCodes.Good);
                       break;
                }

                System.Diagnostics.Debug.WriteLine("{0}: {1}, {2}", "Holo: onNotification: value: " + reworkCode_value + " status: " + reworkCode_status, "value: " + reworkDone_value + " status: " + reworkDone_status, "value: " + reworkStart_value + " status: " + reworkStart_status);
            }
        }

        


    }
}