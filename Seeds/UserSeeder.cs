﻿using System;
using Domotica_API.Models;
using System.Linq;
using System.Threading.Tasks;
using Domotica_API.Controllers;

namespace Domotica_API.Seeds
{
    public class UserSeeder : Seeder
    {
        protected override async Task Seed(DatabaseContext db)
        {
            User newUserJeroen = new User
            {
                name = "Jeroen Groenveld",
                email = "jeroengroenveld@live.nl",
                background_id = 1,
                is_admin = true,
                password = Convert.ToBase64String(UserController.HashPassword("Hoi12345"))
            };
            if (db.Users.SingleOrDefault(x => x.email == newUserJeroen.email) == null)
            {
                db.Add(newUserJeroen);
            }

            User newUserTest = new User
            {
                name = "Mr. Testing oh Test",
                email = "test@live.nl",
                background_id = 1,
                password = Convert.ToBase64String(UserController.HashPassword("Hoi12345"))
            };
            if (db.Users.SingleOrDefault(x => x.email == newUserTest.email) == null)
            {
                db.Add(newUserTest);
            }


            User newUserThijs = new User
            {
                name = "Thijs Bouwes",
                email = "thijsbouwes@gmail.com",
                background_id = 1,
                is_admin = true,
                password = Convert.ToBase64String(UserController.HashPassword("Hoi12345"))
            };
            if (db.Users.SingleOrDefault(x => x.email == newUserTest.email) == null)
            {
                db.Add(newUserThijs);
            }

            await db.SaveChangesAsync();

            //Game game = new Game
            //{
            //    created_at = DateTime.Now,
            //    finished_at = DateTime.Now,
            //    User1 = db.Users.SingleOrDefault(x => x.email == newUserJeroen.email),
            //    UserWinner = db.Users.SingleOrDefault(x => x.email == newUserJeroen.email),
            //    status = GameStatus.finished
            //};
            //db.Add(game);

            //await db.SaveChangesAsync();
        }
    }
}
