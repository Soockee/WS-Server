using System;
using System.IO;

namespace WS_Server
{
    [Serializable]
    class ImageData
    {
        private String filename;
        private byte[] img;

        public ImageData(String filename, String imagePath) {
            this.filename = filename;
            try
            {
                this.img = File.ReadAllBytes(imagePath);
            }
            catch (FileNotFoundException e) {
                throw e;
            }
        } 
        public byte[] getImage() {
            return img;
        }
        public String getFileName()
        {
            return filename;
        }
    }
}
