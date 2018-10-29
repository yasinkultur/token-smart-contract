using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System.Numerics;

namespace Neo.SmartContract
{
    public class KYC : Framework.SmartContract
    {
        public static string[] GetKYCMethods() => new string[] {
            "AddAddress",
            "crowdsale_status",
            "GetBlockHeight",
            "GetGroupMaxContribution",
            "GetGroupNumber",
            "GetGroupUnlockTime",
            "GroupParticipationIsUnlocked",
            "RevokeAddress",
        };

        public static object HandleKYCOperation(string operation, params object[] args)
        {
            // neo-compiler doesn't support switch blocks with too many case statements due to c# compiler optimisations
            // * IL_0004 Call System.UInt32 <PrivateImplementationDetails>::ComputeStringHash(System.String) ---> System.Exception: not supported on neovm now.
            // therefore, extra if statements required for more than 6 operations
            if (operation == "crowdsale_status")
            {
                // test if an address is whitelisted
                if (!Helpers.RequireArgumentLength(args, 1))
                {
                    return false;
                }
                return AddressIsWhitelisted((byte[])args[0]);
            }
            else if (operation == "GetGroupNumber")
            {
                // allow people to check which group they have been assigned to during the whitelist process
                if (!Helpers.RequireArgumentLength(args, 1))
                {
                    return false;
                }
                return GetWhitelistGroupNumber((byte[])args[0]);
            }
            else if (operation == "GroupParticipationIsUnlocked")
            {
                // allow people to check if their group is unlocked (bool)
                if (!Helpers.RequireArgumentLength(args, 1))
                {
                    return false;
                }
                return GroupParticipationIsUnlocked((int)args[0]);
            } else if (operation == "GetBlockHeight")
            {
                // expose a method to retrieve current block height
                return Blockchain.GetHeight();
            }

            switch (operation)
            {
                case "AddAddress":
                    // add an address to the kyc whitelist
                    if (!Helpers.RequireArgumentLength(args, 2))
                    {
                        return false;
                    }
                    return AddAddress((byte[])args[0], (int)args[1]);
                case "GetGroupMaxContribution":
                    // get the maximum amount of NOS that can be purchased for group
                    if (!Helpers.RequireArgumentLength(args, 1))
                    {
                        return false;
                    }
                    return GetGroupMaxContribution((BigInteger)args[0]);
                case "GetGroupUnlockTime":
                    // allow people to check the block height their group will be unlocked (uint)
                    if (!Helpers.RequireArgumentLength(args, 1))
                    {
                        return false;
                    }
                    return GetGroupUnlockTime((BigInteger)args[0]);
                case "RevokeAddress":
                    // remove an address to the kyc whitelist
                    if (!Helpers.RequireArgumentLength(args, 1))
                    {
                        return false;
                    }
                    return RevokeAddress((byte[])args[0]);

            }

            return false;
        }

        /// <summary>
        /// add an address to the kyc whitelist
        /// </summary>
        /// <param name="address"></param>
        public static bool AddAddress(byte[] address, int groupNumber)
        {
            if (address.Length != 20 || groupNumber <= 0 || groupNumber > 4)
            {
                return false;
            }

            if (Helpers.VerifyWitness(ICOTemplate.KycMiddlewareKey))
            {
                StorageMap kycWhitelist = Storage.CurrentContext.CreateMap(StorageKeys.KYCWhitelistPrefix());
                kycWhitelist.Put(address, groupNumber);
                return true;
            }
            return false;
        }

        /// <summary>
        /// determine if the given address is whitelisted by testing if group number > 0
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static bool AddressIsWhitelisted(byte[] address)
        {
            if (address.Length != 20)
            {
                return false;
            }

            BigInteger whitelisted = GetWhitelistGroupNumber(address);
            return whitelisted > 0;
        }

        /// <summary>
        /// get the maximum number of NOS that can be purchased by groupNumber during the public sale
        /// </summary>
        /// <param name="groupNumber"></param>
        /// <returns></returns>
        public static BigInteger GetGroupMaxContribution(BigInteger groupNumber)
        {
            BigInteger maxContribution = 0;
            uint latestTimeStamp = Helpers.GetBlockTimestamp();
            uint publicSaleMaxContribution = (uint)ICOTemplate.MaximumContributionAmount();
            uint publicSaleEndTime = (uint)ICOTemplate.PublicSaleEndTime();

            //If latest block timestamp is larger than presale start and smaller than presale end: check presale tier contributions.
            if (latestTimeStamp >= (uint)ICOTemplate.PresaleStartTime() && latestTimeStamp <= (uint)ICOTemplate.PresaleEndTime())
            {
                //Presale has not ended. Only presale can participate.
                if (groupNumber == 1)
                {
                    //Pre-sale tier 1.
                    maxContribution = (uint)ICOTemplate.PresaleTierOne();
                }
                else if (groupNumber == 2)
                {
                    //Pre-sale tier 2.
                    maxContribution = (uint)ICOTemplate.PresaleTierTwo();
                }
                else if (groupNumber == 3)
                {
                    //Pre-sale tier 3.
                    maxContribution = (uint)ICOTemplate.PresaleTierThree();
                }
                else if(groupNumber == 4)
                {
                    //Tier 4
                    maxContribution = (uint)ICOTemplate.PresaleTierFour();
                }
            }
            //Otherwise we're in the public sale; get the publicSaleMaxContribution
            //publicSaleMaxContribution returns the max contribution based on the presale phase using Helpers.GetPublicSaleMaxContribution()
            else if (groupNumber > 0 && groupNumber <= 4 && latestTimeStamp >= (uint)ICOTemplate.PublicSaleStartTime() && latestTimeStamp <= publicSaleEndTime)
            {
                maxContribution = publicSaleMaxContribution;
            }

            return maxContribution;
        }

        /// <summary>
        /// helper method to retrieve the stored group unlock block height
        /// </summary>
        /// <param name="groupNumber"></param>
        /// <returns></returns>
        public static uint GetGroupUnlockTime(BigInteger groupNumber)
        {
            BigInteger unlockTime = 0;

            if (groupNumber <= 0 || groupNumber > 4)
            {
                return 0;
            }
            else if (groupNumber > 0 && groupNumber <= 4)
            {
                unlockTime = (uint)ICOTemplate.PresaleStartTime();
            }
            return (uint)unlockTime;
        }

        /// <summary>
        /// retrieve the group number the whitelisted address is in
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static BigInteger GetWhitelistGroupNumber(byte[] address)
        {
            if (address.Length != 20)
            {
                return 0;
            }

            StorageMap kycWhitelist = Storage.CurrentContext.CreateMap(StorageKeys.KYCWhitelistPrefix());
            return kycWhitelist.Get(address).AsBigInteger();
        }

        /// <summary>
        /// determine if groupNumber is eligible to participate in public sale yet
        /// </summary>
        /// <param name="groupNumber"></param>
        /// <returns></returns>
        public static bool GroupParticipationIsUnlocked(int groupNumber)
        {
            if (groupNumber <= 0)
            {
                return false;
            }

            uint unlockBlockTime = GetGroupUnlockTime(groupNumber);
            return unlockBlockTime > 0 && unlockBlockTime <= Helpers.GetBlockTimestamp();
        }

        /// <summary>
        /// remove an address from the whitelist
        /// </summary>
        /// <param name="address"></param>
        public static bool RevokeAddress(byte[] address)
        {
            if (address.Length != 20)
            {
                return false;
            }

            if (Helpers.VerifyWitness(ICOTemplate.KycMiddlewareKey))
            {
                StorageMap kycWhitelist = Storage.CurrentContext.CreateMap(StorageKeys.KYCWhitelistPrefix());
                kycWhitelist.Delete(address);
                return true;
            }
            return false;
        }

      
    }
}
