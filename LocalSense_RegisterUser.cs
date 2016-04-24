        private void RealSenseConfiguration()
        {

            // Using the FaceModule
            PXCMFaceModule faceModule;
            PXCMFaceConfiguration faceConfig;

            // Start the SenseManager and session  
            senseManager = PXCMSenseManager.CreateInstance();

            // Enable the color stream
            senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_COLOR, 960, 540, 30);

            // Enable the face module
            senseManager.EnableFace();
            faceModule = senseManager.QueryFace();
            faceConfig = faceModule.CreateActiveConfiguration();

            // Setup face tracking to use both RGB and depth streams
            faceConfig.SetTrackingMode(PXCMFaceConfiguration.TrackingModeType.FACE_MODE_COLOR_PLUS_DEPTH);

            // Enable facial recognition
            recognitionConfig = faceConfig.QueryRecognition();
            recognitionConfig.Enable();

            // Create a local recognition database
            PXCMFaceConfiguration.RecognitionConfiguration.RecognitionStorageDesc recognitionStorageDesc = new PXCMFaceConfiguration.RecognitionConfiguration.RecognitionStorageDesc();
            recognitionStorageDesc.maxUsers = DatabaseUsers;
            recognitionConfig.CreateStorage(DatabaseName, out recognitionStorageDesc);
            recognitionConfig.UseStorage(DatabaseName);
            LoadDatabaseFromFile();
            recognitionConfig.SetRegistrationMode(PXCMFaceConfiguration.RecognitionConfiguration.RecognitionRegistrationMode.REGISTRATION_MODE_CONTINUOUS);
            PXCMFaceConfiguration.RecognitionConfiguration.RecognitionStorageDesc outStorage;
            recognitionConfig.QueryActiveStorage(out outStorage);

            // Apply changes and initialize
            faceConfig.ApplyChanges();
            senseManager.Init();
            faceData = faceModule.CreateOutput();

            // Release resources
            faceConfig.Dispose();
            faceModule.Dispose();
        }

        private void WorkerThread()
        {

            // Loop that Acquires and Releases RealSense data streams
            while (senseManager.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {

                // Acquire the RGB image data
                PXCMCapture.Sample captureSample = senseManager.QuerySample();
                Bitmap frameBitmapRGB;
                PXCMImage.ImageData colorData;
                captureSample.color.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB24, out colorData);
                frameBitmapRGB = colorData.ToBitmap(0, captureSample.color.info.width, captureSample.color.info.height);

                // Get face data
                if (faceData != null)
                {
                    faceData.Update();
                    totalDetectedFaces = faceData.QueryNumberOfDetectedFaces();

                    if (totalDetectedFaces > 0)
                    {
                        // Get the first face detected (index 0)
                        PXCMFaceData.Face face = faceData.QueryFaceByIndex(0);

                        // Process face recognition data
                        if (face != null)
                        {
                            // Retrieve the recognition data instance
                            recognitionData = face.QueryRecognition();

                            // Check if user is registered in the local database
                            if (recognitionData.IsRegistered())
                            {
                                userId = Convert.ToString(recognitionData.QueryUserID());

                                // Store User Data
                                if (doStoreUser)
                                {
                                    // Check if the ID exists in the User Data Store
                                    bool newID = true;
                                    foreach (var userData in userDataStore)
                                    {
                                        if (userData.ID == userId)
                                        {
                                            newID = false;
                                            break;
                                        }
                                    }

                                    if (newID)
                                    {
                                        // Storing a user snapshot
                                        string snapshot = "snapshot" + userId + ".jpg";
                                        frameBitmapRGB.Save(snapshot, System.Drawing.Imaging.ImageFormat.Jpeg);

                                        // Adding the new user to the User Data Store
                                        this.Dispatcher.Invoke((Action)(() =>
                                        {
                                            userDataStore.Add(new UserData() { ID = userId, UserName = tbUserName.Text, ClearanceLevel = tbClearanceLevel.Text, Snapshot = snapshot });
                                        }));

                                    }
                                    doStoreUser = false;
                                    doRegisterLocal = false;
                                }
                            }
                            else
                            {
                                if (doRegisterLocal)
                                {
                                    // Add unregistered user to the local database
                                    recognitionData.RegisterUser();
                                    doRegisterLocal = false;
                                }
                            }
                        }
                    }
                }
            }
        }