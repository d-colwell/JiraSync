﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Tricentis.TCCore.CredentialManager
{
    [Serializable]
    public class Credential
    {
        public string Description { get; set; }
        public string BaseURL { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
