﻿using BotProject.Common;
using BotProject.Common.DigiproService.Digipro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Cors;

namespace BotProject.Web.API
{
    [RoutePrefix("api/dell")]
    public class DellController : ApiController
    {
        // GET: Dell
        [HttpGet]
        public IHttpActionResult Index(string id)
        {
            return Ok();
        }

        //[Route("getdgpservice")]
        //[HttpGet]
        //public IHttpActionResult GetDgpService(string id)
        //{
        //    var Result = DigiproService.GetDetailServiceWarrantyDigipro(id);
        //    return Ok(Result);
        //}

        [Route("getwarranty")]
        [HttpGet]
        public IHttpActionResult Warranty(string id)
        {
            var Result = DellServices.GetAssetHeader(id);
            return Ok(Result);
        }

        [HttpPost]
        public IHttpActionResult Warrantys(string id)
        {
            var Result = DellServices.GetAssetHeader(id);
            //return Request.CreateResponse(HttpStatusCode.OK, Result);
            return Ok(Result);
        }
    }
}
