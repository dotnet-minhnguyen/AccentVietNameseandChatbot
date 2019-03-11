﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotProject.Model.Models
{
    [Table("Bots")]
    public class Bot
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ID { set; get; }

        [Required]
        [MaxLength(256)]
        public string Name { set; get; }

        public DateTime CreatedDate { set; get; }

        [StringLength(128)]
        [Column(TypeName = "nvarchar")]
        public string UserID { set; get; }

        [ForeignKey("UserID")]
        public virtual ApplicationUser User { set; get; }

        public virtual IEnumerable<Card> Cards { set; get; }
    }
}