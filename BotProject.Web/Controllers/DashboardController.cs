﻿using AutoMapper;
using BotProject.Model.Models;
using BotProject.Service;
using BotProject.Web.Infrastructure.Core;
using BotProject.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
namespace BotProject.Web.Controllers
{
    public class DashboardController : BaseController
    {
        private IBotService _botService;
		private IQnAService _qnaService;
        public DashboardController(IErrorService errorService,
									IBotService botService,
									IQnAService qnaService) : base(errorService)
        {
            _botService = botService;
			_qnaService = qnaService;

		}
        [OutputCacheAttribute(VaryByParam = "*", Duration = 0, NoStore = true)]
        public ActionResult Index()
        {
            return View();
        }

        [ChildActionOnly]
        public ActionResult Header()
        {
            ViewBag.UserName = UserInfo.UserName;
			ViewBag.UserID = UserInfo.Id;
			return PartialView();
        }

        [ChildActionOnly]
        //[OutputCache(Duration = 3600, Location = System.Web.UI.OutputCacheLocation.Client)]
        public ActionResult BotCategory()
        {
            var lstBot = _botService.GetListBotByUserID(UserInfo.Id);			
			if(lstBot != null && lstBot.Count() != 0)
			{
				foreach(var item in lstBot)
				{
					item.BotQnAnswers = _qnaService.GetListBotQnAnswerByBotID(item.ID);
				}
			}
            var lstBotViewModel = Mapper.Map<IEnumerable<Bot>, IEnumerable<BotViewModel>>(lstBot);
            return PartialView(lstBotViewModel);
        }


        [ChildActionOnly]
        public ActionResult Footer()
        {
            return PartialView();
        }
    }
}