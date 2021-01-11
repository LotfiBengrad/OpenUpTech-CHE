using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OpenUpDataStructures.Extentions;
using UnityEngine;

namespace OpenUp
{
    using System;
    using System.Threading.Tasks;
    using DataStructures;
    using Networking;
    using Networking.PlayerVisualizer;
    using Utils;
    using Utils.CSharpExtensions;
    using MultiPlayerManager = Networking.PlayerVisualizer.Manager;

    public class ExampleCustomMessage : MonoBehaviour
    {
        public byte[] byteArray;
        
        // Start is fired after the component has been added. 
        private IEnumerator Start()
        {
            // Wait for a connection to have been established.
            yield return DDPConnector.waitForConnection;




            AudioClip clip = Microphone.Start("", true, 10, 44100);

            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            

            // Converts float array to byte array
            byteArray = new byte[samples.Length * 4];
            Buffer.BlockCopy(samples,0,byteArray,0,byteArray.Length);

            //// Converts byte array to float array
            //var samples2 = new float[byteArray.Length / 4];
            //Buffer.BlockCopy(byteArray, 0, samples2, 0, byteArray.Length);

            //Console.WriteLine(samples.SequenceEqual(samples2));

            
            /*
             * een manier vinden om de byte array naar de functie GetData te sturen, get data stuurd dit dan naar senddata dat het dan naar de server stuurd.
             *
             */

            StartCoroutine(SendSomething());

        }

        private IEnumerator SendSomething()
        {
            while (true)
            {
                yield return new WaitForTask(SendData());
                yield return new WaitForSeconds(3);
            }
        }

        private async Task SendData()
        {
            try
            {
                byte[] data = GetData();
                byte[] response = await DDPConnector.Instance.connection.Call.SendCustomMessage("testMessage", data);
                
                HandleResponse(response);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private byte[] GetData()
        {
            const string ownName = nameof(ExampleCustomMessage);
            byte[] data = new byte[BinaryUtils.StringSize(ownName) + 2 * BinaryUtils.BinarySizeOf<TransformStructure>()];

            int idx = 0;

            idx += BinaryUtils.WriteStringToBytes(ownName, data, idx);
            idx += BinaryUtils.WriteStructToBytes(TransformStructure.IDENTITY, data, idx);
            idx += BinaryUtils.WriteStructToBytes(new TransformStructure(transform), data, idx);

            return data;
        }
        private void HandleResponse(byte[] responseData)
        {
            ArraySegment<byte> seg = new ArraySegment<byte>(responseData);
            BinaryUtils.ReadString(seg, out string res);
                
            Debug.Log(res);
        }
    }
}

