using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VotingApp.Helpers
{
    public class PermissionItem
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public static class Helper
    {
        public static readonly int MUNICIPALITY = 2;
        public static readonly int ADMINISTRATIVE_POST = 3;
        public static readonly int VILLAGE = 4;


        public static class Permission
        {
            public const string PERMISSION_VIEW_ACCOUNT = "PERMISSION.VIEW.ACCOUNT";
            public const string PERMISSION_DELETE_USER = "PERMISSION.VIEW.DELETE_USER";

            public const string PERMISSION_ACCESS_AREA = "PERMISSION.ADD.AREA";
            public const string PERMISSION_ACCESS_ADD_AREA = "PERMISSION.ADD.AREA";
            public const string PERMISSION_ACCESS_ADD_POLLING_STATION = "PERMISSION.ADD.POLLING_STATION";
            public const string PERMISSION_ACCESS_ADD_CANDIDATE = "PERMISSION.ADD.CANDIDATE";
            public const string PERMISSION_ACCESS_ADD_VOTING = "PERMISSION.ADD.VOTING";

            public const string PERMISSION_ACCESS_VIEW_LOG = "PERMISSION.VIEW.LOG";

            public static IList<PermissionItem> List()
            {
                return new List<PermissionItem>
                {
                    new PermissionItem { Name=PERMISSION_VIEW_ACCOUNT,Description=PERMISSION_VIEW_ACCOUNT},
                    new PermissionItem { Name=PERMISSION_DELETE_USER,Description=PERMISSION_DELETE_USER},

                    new PermissionItem { Name=PERMISSION_ACCESS_AREA,Description=PERMISSION_ACCESS_AREA},
                    new PermissionItem { Name=PERMISSION_ACCESS_ADD_AREA,Description=PERMISSION_ACCESS_ADD_AREA},
                    new PermissionItem { Name=PERMISSION_ACCESS_ADD_POLLING_STATION,Description=PERMISSION_ACCESS_ADD_POLLING_STATION},
                    new PermissionItem { Name=PERMISSION_ACCESS_ADD_CANDIDATE,Description=PERMISSION_ACCESS_ADD_CANDIDATE},
                    new PermissionItem { Name=PERMISSION_ACCESS_ADD_VOTING,Description=PERMISSION_ACCESS_ADD_VOTING},
                    new PermissionItem { Name=PERMISSION_ACCESS_VIEW_LOG,Description=PERMISSION_ACCESS_VIEW_LOG},

                };
            }
        }

        public static Dictionary<string, string> initializeAreaKeys()
        {
            var dict = new Dictionary<string, string>();

            dict.Add("TLS001", "tl-an");
            dict.Add("TLS002", "tl-al");
            dict.Add("TLS003", "tl-bc");
            dict.Add("TLS004", "tl-bb");
            dict.Add("TLS005", "tl-cl");
            dict.Add("TLS006", "tl-dl");
            dict.Add("TLS007", "tl-er");
            dict.Add("TLS008", "tl-lq");
            dict.Add("TLS009", "tl-bt");
            dict.Add("TLS010", "tl-mf");
            dict.Add("TLS011", "tl-mt");
            dict.Add("TLS012", "tl-am");
            dict.Add("TLS013", "tl-vq");

            return dict;
        }

    }



}
