namespace server
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MongoDB.Driver;
    using DBConnection;
    using OpenUp.Networking;
    using OpenUp.Networking.ServerCalls;

    public partial class MethodCallHandlers : IServerCallMethods
    {
        // ========== HELPER METHODS ==========
        private Organisation GetOrganisation(string organisationID)
        {
            FilterDefinition<Organisation> filter = Builders<Organisation>.Filter.Eq("_id", organisationID);
            List<Organisation> orgList = MongoConnection.Instance.organisations.Find(filter).ToList();
            
            return orgList.Count == 0 ? null : orgList[0];
        }

        private async void removeMemberFromOrganisation(string organisationID, string userID)
        {
            FilterDefinition<Organisation> organisationFilter = Builders<Organisation>.Filter.Eq("_id", organisationID);
            UpdateDefinition<Organisation> update = Builders<Organisation>.Update.PullFilter(
                "members",
                Builders<Organisation.Member>.Filter.Eq("id", userID)
            );
            await MongoConnection.Instance.organisations.FindOneAndUpdateAsync(organisationFilter, update);
        }

        // =========== SERVER CALLS ===========
        public async Task<Exception> CreateOrganisation(Organisation organisation)
        {
            try
            {
                // New organisations need to have a name
                if (organisation.name == null)
                    throw new ArgumentException("New organisations need to have a name");

                // Force default values
                organisation.id = Guid.NewGuid().ToString();
                organisation.members = new List<Organisation.Member>();
                organisation.invites = new List<Organisation.Invite>();

                // Make sure it has a profile, using default values as necessary
                Organisation.Profile defaultProfile = new Organisation.Profile {
                    description = "A new organisation.",
                    picture = null
                };
                if (organisation.profile == null)
                    organisation.profile = defaultProfile;
                else if (organisation.profile.description == null)
                    organisation.profile.description = defaultProfile.description;
                // NOTE: picture will remain null for default, or whatever the client supplied

                // Add current user as owner
                organisation.members.Add(new Organisation.Member {
                    id = connection.user.id,
                    role = "owner"
                });

                // Insert into database
                await MongoConnection.Instance.organisations.InsertOneAsync(organisation);
                Console.WriteLine($"Inserted new organisation '{organisation.name}' into database.");
                return null;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception occured - {exception.GetType()}: {exception.Message}");
                Console.WriteLine(exception.StackTrace);
                return exception;
            }
        }

        public async Task<Exception> DeleteOrganisation(string organisationID)
        {
            try
            {
                Organisation org = GetOrganisation(organisationID);

                // Check if organisation exists
                if (org == null)
                    throw new ArgumentException($"Attempting to delete non-existent organisation with id '{organisationID}'.");

                // Check if user is owner (only owner has permission to delete)
                int memberIndex = org.members.FindIndex(m => m.id == connection.user.id);
                if (memberIndex == -1 || org.members[memberIndex].role != "owner")
                {
                    throw new PermissionDeniedException(
                        connection.user.id,
                        $"Deleting organisation '{organisationID}' without being the owner."
                    );
                }

                // Delete organisation from database
                FilterDefinition<Organisation> filter = Builders<Organisation>.Filter.Eq("_id", organisationID);
                await MongoConnection.Instance.organisations.DeleteOneAsync(filter);
                Console.WriteLine($"Removed organisation with id '{organisationID}' from database.");
                return null;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception occured - {exception.GetType()}: {exception.Message}");
                Console.WriteLine(exception.StackTrace);
                return exception;
            }
        }

        public async Task<Exception> LeaveOrganisation(string organisationID, string userID)
        {
            try
            {
                Organisation org = GetOrganisation(organisationID);

                // Check if organisation exists
                if (org == null)
                    throw new ArgumentException($"An organisation with id '{organisationID}' does not exist.");

                // Check if user is in organisation
                int memberIndex = org.members.FindIndex(m => m.id == userID);
                if (memberIndex == -1)
                    throw new ArgumentException($"User `{userID}` is not a member of organisation `{organisationID}`");

                // Leaving organisation voluntarily
                if (userID == connection.user.id)
                {
                    // Can't leave if owner
                    if (org.members[memberIndex].role == "owner")
                        throw new ArgumentException($"Organisation's owner can't leave.");

                    // Remove member from organisation
                    removeMemberFromOrganisation(organisationID, userID);
                    Console.WriteLine($"User '{userID}' has left organisation '{organisationID}'");
                }

                // Kicking other users
                else
                {
                    // Find current user in organisation
                    int currentUserIndex = org.members.FindIndex(m => m.id == connection.user.id);
                    if (currentUserIndex == -1)
                    {
                        throw new PermissionDeniedException(
                            connection.user.id,
                            $"Kicking someone from organisation '{organisationID}' without being a member of that organisation themselves"
                        );
                    }

                    // Only owner and admin can kick
                    if (org.members[currentUserIndex].role != "owner" && org.members[currentUserIndex].role != "admin")
                    {
                        throw new PermissionDeniedException(
                            connection.user.id,
                            $"Kicking someone from organisation '{organisationID}' without being admin or owner"
                        );
                    }

                    // Can't kick the owner
                    if (org.members[memberIndex].role == "owner")
                    {
                        throw new PermissionDeniedException(
                            connection.user.id,
                            $"Trying to kick the owner of organisation '{organisationID}'. Owners can't be kicked."
                        );
                    }

                    // Remove kicked member from the organisation
                    removeMemberFromOrganisation(organisationID, userID);
                    Console.WriteLine($"User '{userID}' was kicked from organisation '{organisationID}'");
                }

                return null;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception occured - {exception.GetType()}: {exception.Message}");
                Console.WriteLine(exception.StackTrace);
                return exception;
            }
        }

        public async Task<Exception> InviteToOrganisation(string organisationID, string userID, string message)
        {
            try
            {
                Organisation org = GetOrganisation(organisationID);

                // Check if organisation exists
                if (org == null)
                    throw new ArgumentException($"An organisation with id '{organisationID}' does not exist.");

                // Check if user exists
                FilterDefinition<User> userFilter = Builders<User>.Filter.Eq("_id", userID);
                if (MongoConnection.Instance.users.Find(userFilter).ToList().Count == 0)
                    throw new ArgumentException($"Trying to invite non-existing user '{userID}' to organisation '{organisationID}'");

                // Check if user is in organisation
                int memberIndex = org.members.FindIndex(m => m.id == userID);
                if (memberIndex > -1)
                    throw new ArgumentException($"User `{userID}` is already a member of organisation `{organisationID}`");

                // Only owner and admin can invite
                int currentUserIndex = org.members.FindIndex(m => m.id == connection.user.id);
                if (
                    currentUserIndex == -1 || !(
                        org.members[currentUserIndex].role == "owner" ||
                        org.members[currentUserIndex].role == "admin"
                    )
                )
                {
                    throw new PermissionDeniedException(
                        connection.user.id,
                        $"Inviting someone to organisation '{organisationID}' without being admin or owner (or a member in the first place)."
                    );
                }

                // Check if a pending invite for the specified user already exists
                if (org.invites.Find(m => m.receiverID == userID && m.status == Organisation.Invite.Status.PENDING) != null)
                    throw new ArgumentException($"User `{userID}` already has a pending invite for organisation `{organisationID}`");

                // Create invite object
                Organisation.Invite invite = new Organisation.Invite {
                    date = DateTime.Now,
                    senderID = connection.user.id,
                    receiverID = userID,
                    message = message == "" ? "Come join our organisation!" : message,
                    status = Organisation.Invite.Status.PENDING
                };
                
                // Insert invite into database
                FilterDefinition<Organisation> organisationFilter = Builders<Organisation>.Filter.Eq("_id", organisationID);
                UpdateDefinition<Organisation> update = Builders<Organisation>.Update.Push("invites", invite);
                await MongoConnection.Instance.organisations.FindOneAndUpdateAsync(organisationFilter, update);

                Console.WriteLine($"User '{userID}' has been invited to join organisation '{organisationID}'");
                return null;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception occured - {exception.GetType()}: {exception.Message}");
                Console.WriteLine(exception.StackTrace);
                return exception;
            }
        }

        public async Task<Exception> OrganisationInviteResponse(string organisationID, bool accepted)
        {
            try
            {
                Organisation org = GetOrganisation(organisationID);

                // Check if organisation exists
                if (org == null)
                    throw new ArgumentException($"An organisation with id '{organisationID}' does not exist.");

                // Check if user has a pending invite for the specified organisation
                if (org.invites.Find(m => m.receiverID == connection.user.id && m.status == Organisation.Invite.Status.PENDING) == null)
                    throw new ArgumentException($"User '{connection.user.id}' does not have a pending invite for organisation '{organisationID}'");

                // Check if user is already in organisation (this should NEVER happen, but better to check just in case)
                if (org.members.Find(m => m.id == connection.user.id) != null)
                    throw new ArgumentException($"User `{connection.user.id}` is already a member of organisation `{organisationID}`");

                // Register reply in database
                FilterDefinition<Organisation> organisationFilter = Builders<Organisation>.Filter.Eq("_id", organisationID);
                FilterDefinition<Organisation> filter = Builders<Organisation>.Filter.And(
                    organisationFilter,
                    Builders<Organisation>.Filter.ElemMatch(
                        x => x.invites,
                        f => f.receiverID == connection.user.id && f.status == Organisation.Invite.Status.PENDING
                    )
                );
                UpdateDefinition<Organisation> inviteUpdate = Builders<Organisation>.Update.Set(
                    o => o.invites[-1].status,
                    accepted ? Organisation.Invite.Status.ACCEPTED : Organisation.Invite.Status.DECLINED
                );
                await MongoConnection.Instance.organisations.UpdateOneAsync(filter, inviteUpdate);

                // Insert user into memebers if accepted
                if (accepted)
                {
                    // Create member object
                    Organisation.Member member = new Organisation.Member {
                        id = connection.user.id,
                        role = "member"
                    };

                    // Insert into database
                    UpdateDefinition<Organisation> memberUpdate = Builders<Organisation>.Update.Push("members", member);
                    await MongoConnection.Instance.organisations.FindOneAndUpdateAsync(organisationFilter, memberUpdate);
                }

                string response = accepted ? "accepted" : "declined";
                Console.WriteLine($"User '{connection.user.id}' has {response} the invitation to join organisation '{organisationID}'.");
                return null;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception occured - {exception.GetType()}: {exception.Message}");
                Console.WriteLine(exception.StackTrace);
                return exception;
            }
        }

        public async Task<Exception> RevokeOrganisationInvite(string organisationID, string userID)
        {
            try
            {
                Organisation org = GetOrganisation(organisationID);

                // Check if organisation exists
                if (org == null)
                    throw new ArgumentException($"An organisation with id '{organisationID}' does not exist.");

                // Retrieve pending invite for user
                Organisation.Invite invite =  org.invites.Find(m => m.receiverID == userID && m.status == Organisation.Invite.Status.PENDING);
                if (invite == null)
                    throw new ArgumentException($"User `{userID}` has no pending invite for organisation `{organisationID}`");

                // Only owner and admin that sent the invite can revoke it
                int currentUserIndex = org.members.FindIndex(m => m.id == connection.user.id);
                bool isMember = currentUserIndex > -1;
                bool isOwner = isMember ? org.members[currentUserIndex].role == "owner" : false;
                bool isAdmin = isMember ? org.members[currentUserIndex].role == "admin" : false;
                bool isAdminThatSentInvite = isAdmin && invite.senderID == connection.user.id;

                if (!isMember || !(isOwner || isAdminThatSentInvite))
                {
                    throw new PermissionDeniedException(
                        connection.user.id,
                        $"Revoking invite of user '{userID}' to organisation '{organisationID}' without being owner or the admin who sent it (or a member in the first place)."
                    );
                }

                // Set invite to revoked in database
                FilterDefinition<Organisation> filter = Builders<Organisation>.Filter.And(
                    Builders<Organisation>.Filter.Eq("_id", organisationID),
                    Builders<Organisation>.Filter.ElemMatch(
                        x => x.invites,
                        f => f.receiverID == userID && f.status == Organisation.Invite.Status.PENDING
                    )
                );
                UpdateDefinition<Organisation> update = Builders<Organisation>.Update.Set(
                    o => o.invites[-1].status,
                    Organisation.Invite.Status.REVOKED
                );
                await MongoConnection.Instance.organisations.UpdateOneAsync(filter, update);

                Console.WriteLine($"Invite for user '{userID}' to join organisation '{organisationID}' revoked.");
                return null;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception occured - {exception.GetType()}: {exception.Message}");
                Console.WriteLine(exception.StackTrace);
                return exception;
            }
        }

        public async Task<Exception> OrganisationSetRole(string organisationID, string userID, string role)
        {
            try
            {
                Organisation org = GetOrganisation(organisationID);

                // Check if organisation exists
                if (org == null)
                    throw new ArgumentException($"An organisation with id '{organisationID}' does not exist.");

                // Only owner can change roles
                int currentUserIndex = org.members.FindIndex(m => m.id == connection.user.id);
                bool isMember = currentUserIndex > -1;
                bool isOwner = isMember ? org.members[currentUserIndex].role == "owner" : false;
                if (!isOwner)
                {
                    throw new PermissionDeniedException(
                        connection.user.id,
                        $"Changing the role of user '{userID}' in organisation '{organisationID}' without being its owner (or a member in the first place)."
                    );
                }

                // Check if target user is a member of the organisation
                int memberIndex = org.members.FindIndex(m => m.id == userID);
                if (memberIndex == -1)
                    throw new ArgumentException($"User `{userID}` is not a member of organisation `{organisationID}`");

                // Can't transfer ownership through this server call
                if (role == "owner")
                    throw new ArgumentException("Can't transfer ownership, use OrganisationTransferOwnership instead.");

                // Set invite to revoked in database
                FilterDefinition<Organisation> filter = Builders<Organisation>.Filter.And(
                    Builders<Organisation>.Filter.Eq("_id", organisationID),
                    Builders<Organisation>.Filter.Eq("members.id", userID)
                );
                UpdateDefinition<Organisation> update = Builders<Organisation>.Update.Set("members.$.role", role);
                await MongoConnection.Instance.organisations.UpdateOneAsync(filter, update);

                Console.WriteLine($"User '{userID}'`s role in organisation '{organisationID}' set to '{role}'.");
                return null;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception occured - {exception.GetType()}: {exception.Message}");
                Console.WriteLine(exception.StackTrace);
                return exception;
            }
        }

        public async Task<Exception> OrganisationTransferOwnership(string organisationID, string userID)
        {
            try
            {
                Organisation org = GetOrganisation(organisationID);

                // Check if organisation exists
                if (org == null)
                    throw new ArgumentException($"An organisation with id '{organisationID}' does not exist.");

                // Only owner can transfer ownership
                int currentUserIndex = org.members.FindIndex(m => m.id == connection.user.id);
                bool isMember = currentUserIndex > -1;
                bool isOwner = isMember ? org.members[currentUserIndex].role == "owner" : false;
                if (!isOwner)
                {
                    throw new PermissionDeniedException(
                        connection.user.id,
                        $"Transferring ownership of organisation '{organisationID}' to user '{userID}' without being its owner (or a member in the first place)."
                    );
                }

                // Check if target user is a member of the organisation
                int memberIndex = org.members.FindIndex(m => m.id == userID);
                if (memberIndex == -1)
                    throw new ArgumentException($"User `{userID}` is not a member of organisation `{organisationID}`");

                // Make new user owner
                FilterDefinition<Organisation> userFilter = Builders<Organisation>.Filter.And(
                    Builders<Organisation>.Filter.Eq("_id", organisationID),
                    Builders<Organisation>.Filter.Eq("members.id", userID)
                );
                UpdateDefinition<Organisation> ownerUpdate = Builders<Organisation>.Update.Set("members.$.role", "owner");
                await MongoConnection.Instance.organisations.UpdateOneAsync(userFilter, ownerUpdate);

                // Make old owner admin instead
                FilterDefinition<Organisation> oldOwnerFilter = Builders<Organisation>.Filter.And(
                    Builders<Organisation>.Filter.Eq("_id", organisationID),
                    Builders<Organisation>.Filter.Eq("members.id", connection.user.id)
                );
                UpdateDefinition<Organisation> adminUpdate = Builders<Organisation>.Update.Set("members.$.role", "admin");
                await MongoConnection.Instance.organisations.UpdateOneAsync(oldOwnerFilter, adminUpdate);

                Console.WriteLine($"Ownership of organisation '{organisationID}' transferred to user '{userID}'");
                return null;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception occured - {exception.GetType()}: {exception.Message}");
                Console.WriteLine(exception.StackTrace);
                return exception;
            }
        }
    }
}
