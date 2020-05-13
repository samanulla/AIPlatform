﻿using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Luna.Clients.Controller
{
    public class PredictionController : IController
    {
        public PredictionController()
        { 
        }

        public string GetName()
        {
            return "predict";
        }

        public string GetUrlTemplate()
        {
            return "/predict";
        }

        public string GetMethod()
        {
            return "POST";
        }

        public string GetPath(string productId, string deploymentId)
        {
            return $"/api/products/{productId}/deployments/{deploymentId}";
        }

        public string GetBaseUrl()
        {
            return "https://lunamgmtprod.azurewebsites.net";
        }
    }
}