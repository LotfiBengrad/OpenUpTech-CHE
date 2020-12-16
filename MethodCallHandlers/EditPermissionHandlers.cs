namespace server
{
    using OpenUp.Networking.ServerCalls;
    using OpenUp.Updating;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    
    public partial class MethodCallHandlers : IServerCallMethods
    {
        public async Task<bool> RequestEditPermission(UpdatePath path)
        {
            if (connection.session == null) throw new NotSupportedException("No permissions when you have no session");

            if (connection.session.permissionRegistry.IsUnclaimed(path))
            {
                connection.session.permissionRegistry[connection.id] = path;

                return true;
            }

            return false;
        }

        public async Task ReleaseEditPermission()
        {
            if (connection.session == null) throw new NotSupportedException("No permissions when you have no session");

            connection.session.permissionRegistry.Remove(connection.id);
        }
    }
}