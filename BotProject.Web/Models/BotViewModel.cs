﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace BotProject.Web.Models
{
    public class BotViewModel
    {
        public int ID { set; get; }

        [Required]
        public string Name { set; get; }

		public string Alias { set; get; }

        public DateTime? CreatedDate { set; get; }

        public string CreatedBy { set; get; }

        public DateTime? UpdatedDate { set; get; }

        public string UpdatedBy { set; get; }

        public string MetaKeyword { set; get; }

        public string MetaDescription { set; get; }

        public bool Status { set; get; }

        public string UserID { set; get; }

        public virtual ApplicationUserViewModel User { set; get; }

        public virtual IEnumerable<CardViewModel> Cards { set; get; }

        public virtual IEnumerable<QuestionGroupViewModel> QuestionGroups { set; get; }

        public virtual IEnumerable<AIMLViewModel> AIMLs { set; get; }

		public virtual IEnumerable<FormQuestionAnswerViewModel> FormQuestionAnswers { set; get; }
    }
}