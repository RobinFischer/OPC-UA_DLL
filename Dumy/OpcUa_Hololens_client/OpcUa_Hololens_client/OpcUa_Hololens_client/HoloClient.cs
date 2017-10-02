using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        public bool setEndpoint(string endpointURL)
        {
            return false;
        }

        public bool connect(string username, string password, string reworkCode_NodeId, string reworkDone_NodeId, string reworkSinpos_NodeId, int publishInterval)
        {
            return false;
        }

        public bool disconnect()
        { 
            return false;
        }

        public bool write(string nodeId, string valueToWrite)
        {
            return false;
        }

        public string read(string nodeId)
        {
            return null;
        }


    }
}
