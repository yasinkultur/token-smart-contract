using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Neo.SmartContract
{
    public class EthSale : Framework.SmartContract
    {

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> transfer;

        [DisplayName("refundEth")]
        public static event Action<string, BigInteger> refundEth;

        /// <summary>
        /// MintTokensEth is called when a the ETH contribution listener server triggers an Ether receive event
        /// </summary>
        /// <returns></returns>
        public static bool MintTokensEth(string ethAddress, byte[] neoAddress, ulong ethReceived)
        {

            object[] transactionData = Helpers.GetEthTransactionAndSaleData(ethReceived, ethAddress, neoAddress);
            Transaction tx = (Transaction)transactionData[0];
            byte[] sender = (byte[])transactionData[1];
            byte[] receiver = (byte[])transactionData[2];
            BigInteger whiteListGroupNumber = (BigInteger)transactionData[5];
            BigInteger crowdsaleAvailableAmount = (BigInteger)transactionData[6];
            BigInteger groupMaximumContribution = (BigInteger)transactionData[7];
            BigInteger totalTokensPurchased = (BigInteger)transactionData[8] * NEP5.factor;
            BigInteger totalContributionBalance = (BigInteger)transactionData[9];

            if (!CanETHUserParticipateInSale(transactionData))
            {
                refundEth(ethAddress, ethReceived);
                Runtime.Notify("MintTokensEth() CanUserParticipate failed", false);
                return false;
            }

            if (Helpers.GetBlockTimestamp() >= ICOTemplate.PublicSaleEndTime())
            {
                refundEth(ethAddress, ethReceived);
                Runtime.Notify("MintTokensEth() failed. Token Sale is closed.", false);
                return false;
            }

            byte[] lastTransactionHash = Storage.Get(Storage.CurrentContext, StorageKeys.MintTokensEthLastTX());
            if (lastTransactionHash == tx.Hash)
            {
                // ensure that minTokens doesnt process the same transaction more than once
                Runtime.Notify("MintTokensEth() not processing duplicate tx.Hash", tx.Hash);
                return false;
            }
            
            BigInteger tokenTotalSupply = NEP5.TotalSupply();

            Storage.Put(Storage.CurrentContext, StorageKeys.MintTokensEthLastTX(), tx.Hash);
            Runtime.Notify("MintTokensEth() receivedETH", ethReceived);

            BigInteger senderAmountSubjectToVesting = TokenSale.SubjectToVestingPeriod(sender);
            BigInteger newTokenBalance = NEP5.BalanceOf(sender) + totalTokensPurchased + senderAmountSubjectToVesting;

            Helpers.SetBalanceOf(sender, newTokenBalance);
            Helpers.SetBalanceOfSaleContribution(sender, totalContributionBalance);
            Helpers.SetTotalSupply(totalTokensPurchased);

            transfer(null, sender, totalTokensPurchased);
            return true;
        }

        /// <summary>
        /// determine if ETH  user can participate in the token sale 
        /// </summary>
        /// <param name="sender"></param>
        /// <returns></returns>
        public static bool CanETHUserParticipateInSale(object[] transactionData)
        {
            Transaction tx = (Transaction)transactionData[0];
            byte[] sender = (byte[])transactionData[1];
            byte[] receiver = (byte[])transactionData[2];
            string ethAddress = (string)transactionData[3];
            ulong receivedETH = (ulong)transactionData[4];
            BigInteger whiteListGroupNumber = (BigInteger)transactionData[5];
            BigInteger crowdsaleAvailableAmount = (BigInteger)transactionData[6];
            BigInteger groupMaximumContribution = (BigInteger)transactionData[7];
            BigInteger totalTokensPurchased = (BigInteger)transactionData[8];
            BigInteger totalContributionBalance = (BigInteger)transactionData[9];

            if (whiteListGroupNumber <= 0)
            {
                Runtime.Notify("CanUserParticipate() sender is not whitelisted", sender);
                return false;
            }

            if (!KYC.GroupParticipationIsUnlocked((int)whiteListGroupNumber))
            {
                Runtime.Notify("CanUserParticipate() sender cannot participate yet", sender);
                return false;
            }

            if (crowdsaleAvailableAmount <= 0)
            {
                // total supply has been exhausted
                Runtime.Notify("CanUserParticipate() crowdsaleAvailableAmount is <= 0", crowdsaleAvailableAmount);
                return false;
            }

            if (totalContributionBalance > groupMaximumContribution)
            {
                // don't allow this purchase exceed the group cap
                Runtime.Notify("CanUserParticipate() senders purchase in ETH will exceed maxContribution cap", sender, totalContributionBalance, groupMaximumContribution);
                return false;
            }

            return true;
        }

    }
}