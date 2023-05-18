﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Cloud.SecretManager.V1;
using Google.Cloud.Video.LiveStream.V1;
using PallyCon;
using Google.Protobuf.Collections;

namespace PallyCon
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string pallyconKmsUrl = "https://kms.pallycon.com/v2/cpix/pallycon/getKey/";
            string pallyconEncToken = "<pallycon-enc-token>";
            string contentId = "<content-id>";
            string projectId = "<your-project-id>";
            string outputBucketUri = "<your-bucket-uri>";   // e.g.) gs://<bucket_name>/path/to/output
            // Region for Live Stream API input Endpoint.
            // For the best performance results, make sure you create your input endpoint and channel as close as possible
            // to the location where the live stream files are stored in Cloud Storage.
            // See the following link for the location you can set up. https://cloud.google.com/livestream/docs/locations#regions
            string locationId = "<your-location-id>";

            // Resource id for each.
            // Each ID cannot be duplicated, so if you create one, you must delete it to create another with the same ID.
            string inputId = "pallycon-sample-input";
            string channelId = "pallycon-sample-channel";
            string secretId = "pallycon-sample-secret";

            var widevineMuxStreams = new RepeatedField<string> { "fmp4_widevine_video", "fmp4_widevine_audio" };
            var playreadyMuxStreams = new RepeatedField<string> { "fmp4_playready_video", "fmp4_playready_audio" };
            var fairplayMuxStreams = new RepeatedField<string> { "fmp4_fairplay_video", "fmp4_fairplay_audio" };

            try
            {
                // Make a Secret Key payload by getting the packaging information from the PallyCon KMS server
                string secretPayload = PallyConHelper.GetSecretKeyDataFromPallyConKMS(pallyconKmsUrl, pallyconEncToken, contentId,
                    widevineMuxStreams, playreadyMuxStreams, fairplayMuxStreams);                

                // First, create secret to set the encryption rules
                SecretManagerClient secretManagerClient = new SecretManagerClient(projectId);
                Secret secret = secretManagerClient.CreateSecret(secretId);
                SecretVersion secretVersion = secretManagerClient.AddSecretVersion(secret.SecretName, secretPayload);

                // Create an input endpoint
                LiveStreamAPIClient liveStreamAPIClient = new LiveStreamAPIClient(projectId, locationId);
                Input input = await liveStreamAPIClient.CreateInputAsync(inputId);
                Console.WriteLine("RTMP input endpoint uri : " + input.Uri);

                // Create and start a channel
                Console.WriteLine("Creating a channel...");
                Channel channel = await liveStreamAPIClient.CreateChannelAsync(channelId, inputId, outputBucketUri, secretVersion.Name, 
                    widevineMuxStreams, playreadyMuxStreams, fairplayMuxStreams);
                Console.WriteLine("Starting a channel...");
                await liveStreamAPIClient.StartChannelAsync(channelId);
                Console.WriteLine("Channel starting is done.");

                channel = liveStreamAPIClient.GetChannel(channelId);
                string streamingState = channel.StreamingState.ToString();
                Console.WriteLine("\nStreaming state : " + streamingState + "\n");

                if (streamingState == "StreamingError")
                {
                    // Failed to start the channel. Please check the error details.
                    Console.WriteLine("Error Details : " + channel.StreamingError.ToString());
                }
                else
                {
                    if (streamingState == "AwaitingInput")
                        Console.WriteLine("Now the channel is ready, send a input stream to the input endpoint to generate the live stream.");

                    Console.WriteLine("Press any key to exit and delete the resources.");
                    Console.ReadLine();
                }

                // Delete created resources
                Console.WriteLine("Deleting all the created resources..");
                await liveStreamAPIClient.StopChannelAsync(channelId);
                await liveStreamAPIClient.DeleteChannelAsync(channelId);
                await liveStreamAPIClient.DeleteInputAsync(inputId);
                secretManagerClient.DeleteSecret(secretId);
            }
            catch (Exception ex) 
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
