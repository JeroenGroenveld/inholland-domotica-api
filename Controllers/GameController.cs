﻿using System;
using Domotica_API.Middleware;
using Domotica_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Domotica_API.Controllers
{
    [MiddlewareFilter(typeof(TokenAuthorize))]
    [Route(Config.App.API_ROOT_PATH + "/game")]
    public class GameController : ApiController
    {
        public GameController(DatabaseContext db) : base(db) { }

        #region "HTTP Requests"
        [HttpGet]
        public IActionResult GetUserGameInfo()
        {
            object result = new
            {
                invites = UserInvites(),
                started = UserGamesStarted(),
                lobby = GameLobbyList(),
                finished = UserGamesFinished(),
                stats = UserStats()
            };
            return Ok(result);
        }

        [HttpGet("{id}")]
        public IActionResult GetGameInfo(int id)
        {
            Result result = this.GameInfo(id);
            return result.ResultFunc(result.Data);
        }

        [HttpGet("user/stats")]
        public IActionResult GetUserStats()
        {
            return Ok(UserStats());
        }

        [HttpGet("user/invites")]
        public IActionResult GetUserInvites()
        {
            return Ok(UserInvites());
        }

        [HttpGet("user/started")]
        public IActionResult GetUserGamesStarted()
        {
            return Ok(UserGamesStarted());
        }

        [HttpGet("user/finished")]
        public IActionResult GetUserGamesFinished()
        {
            return Ok(UserGamesFinished());
        }

        [HttpGet("list")]
        public IActionResult GetGameLobbyList()
        {
            return Ok(GameLobbyList());
        }

        [HttpPost("create")]
        public IActionResult PostCreateGame([FromBody] Validators.GameCreate gameCreate)
        {
            Result result = this.CreateGame(gameCreate);
            return result.ResultFunc(result.Data);
        }

        [HttpPut("join")]
        public IActionResult UpdateJoinGame([FromBody] Validators.GameJoin gameJoin)
        {
            Result result = this.JoinGame(gameJoin);
            return result.ResultFunc(result.Data);
        }

        [HttpPost("move/create")]
        public IActionResult PostCreateMove([FromBody] Validators.Move move)
        {
            Result result = this.CreateMove(move);
            return result.ResultFunc(result.Data);
        }
        #endregion


        #region "Helper functions for managing a game"
        private User CheckGameForWin(Game game)
        {
            //Get moves for each user.
            List<Move> User1Moves = game.Moves.Where(x => x.user_id == game.user1_id).ToList();
            List<Move> User2Moves = game.Moves.Where(x => x.user_id == game.user2_id).ToList();

            if (this.CheckMovesForWin(User1Moves))
            {
                return game.User1;
            }

            if (this.CheckMovesForWin(User2Moves))
            {
                return game.User2;
            }

            return null;
        }

        private bool CheckMovesForWin(List<Move> moves)
        {
            bool[] grid = new bool[8];
            foreach (Move move in moves)
            {
                grid[move.position] = true;
            }

            //check top horizontal row.
            if (grid[0] && grid[1] && grid[2])
            {
                return true;
            }

            //check middle horizontal row
            if (grid[3] && grid[4] && grid[5])
            {
                return true;
            }

            //check bottom horizontal row
            if (grid[6] && grid[7] && grid[8])
            {
                return true;
            }

            //check left vertical row
            if (grid[0] && grid[3] && grid[6])
            {
                return true;
            }

            //check middle vertical row
            if (grid[1] && grid[4] && grid[7])
            {
                return true;
            }

            //check right vertical row
            if (grid[2] && grid[5] && grid[8])
            {
                return true;
            }

            //check left - right diagonal row
            if (grid[0] && grid[4] && grid[8])
            {
                return true;
            }

            //check right - left diagonal row
            if (grid[2] && grid[4] && grid[6])
            {
                return true;
            }

            return false;
        }

        private bool CheckPosition(Game game, int position)
        {
            List<Move> moves = this.db.Moves.Where(x => x.game_id == game.id).ToList();
            foreach (Move move in moves)
            {
                //If the position is already found in the existing moves then.
                if (move.position == position)
                {
                    return false;
                }
            }

            return true;
        }

        private User DecideTurn(Game game)
        {
            int User1Moves = game.Moves.Where(x => x.user_id == game.user1_id).ToList().Count;
            int User2Moves = game.Moves.Where(x => x.user_id == game.user2_id).ToList().Count;

            //If the turns are are equal then it's player 1 his turn.
            //So for example when a game has started both turns are 0. This means player one can start.
            if (User1Moves == User2Moves)
            {
                return game.User1;
            }
            //If the turn are not equal then it's player 2 his turn.
            //So when player 1 has 1 turn and player 2 has 0 turn then it's players 2 his turn.
            else
            {
                return game.User2;
            }
        }
        #endregion


        #region "Create/Update functions"
        public Result CreateGame(Validators.GameCreate gameCreate)
        {
            if (ModelState.IsValid == false)
            {
                return new Result { ResultFunc = this.BadRequest, Data = "Incorrect post data" };
            }

            User user = (User)HttpContext.Items["user"];
            User user_2 = this.db.Users.SingleOrDefault(x => x.email == gameCreate.player2_email);

            if (gameCreate.player2_email != null && user_2 == null)
            {
                return new Result { ResultFunc = this.BadRequest, Data = "The user you're trying to invite does not exist." };
            }

            int status = (user_2 == null) ? GameStatus.waiting_join : GameStatus.waiting_invite;

            //Check if the user is trying to invite himself.
            if (user == user_2)
            {
                return new Result { ResultFunc = this.BadRequest, Data = "You can't play against yourself." };
            }

            Game game = new Game
            {
                User1 = user,
                User2 = user_2,
                status = status
            };

            this.db.Add(game);
            this.db.SaveChanges();

            return new Result { ResultFunc = this.Ok, Data = this.FilterGameResults(game) };
        }

        public Result JoinGame(Validators.GameJoin gameJoin)
        {
            if (ModelState.IsValid == false)
            {
                return new Result { ResultFunc = this.BadRequest, Data = "Incorrect post data"};
            }

            User user = (User)HttpContext.Items["user"];
            Game game = this.db.Games.Where(x => x.id == gameJoin.id).Include(x => x.User1).FirstOrDefault();

            //Check if game exists.
            if (game == null)
            {
                return new Result { ResultFunc = this.BadRequest, Data = "Game does not exist." };
            }

            //Check if the user is trying to join a game that he created.
            if (game.User1 == user)
            {
                return new Result { ResultFunc = this.BadRequest, Data = "You can't play against yourself." };
            }

            //Check if the game is waiting for an invited user.
            if (game.status == GameStatus.waiting_invite)
            {
                //Check if the user that tries to join is indeed the invited user.
                if (game.user2_id == user.id)
                {
                    game.status = GameStatus.started;
                    this.db.SaveChanges();
                    return new Result { ResultFunc = this.Ok, Data = this.FilterGameResults(game) };
                }
                return new Result { ResultFunc = this.BadRequest, Data = "Can't join game, you were not invited." };
            }

            //Check if the game is waiting for a random user.
            if (game.status == GameStatus.waiting_join)
            {
                game.status = GameStatus.started;
                game.User2 = user;
                this.db.SaveChanges();
                return new Result { ResultFunc = this.Ok, Data = this.FilterGameResults(game) };
            }

            //Game status is probably started or finished.
            return new Result { ResultFunc = this.BadRequest, Data = "Can't join game. Game is either finished or started." };
        }

        public Result CreateMove(Validators.Move move)
        {
            if (ModelState.IsValid == false)
            {
                return new Result { ResultFunc = this.BadRequest, Data = "Incorrect post data." };
            }

            //Check if game exists.
            Game game = this.db.Games.Where(x => x.id == move.game_id).Include(x => x.User1).Include(x => x.User2).Include(x => x.Moves).SingleOrDefault();
            if (game == null)
            {
                return new Result { ResultFunc = this.BadRequest, Data = "Game does not exist." };
            }

            //Check if the user is a player of this game.
            User user = (User)HttpContext.Items["user"];
            if (game.User1 != user && game.User2 != user)
            {
                return new Result { ResultFunc = this.BadRequest, Data = "This is not your game." };
            }

            //Check if the game has started.
            if (game.status != GameStatus.started)
            {
                switch (game.status)
                {
                    case GameStatus.waiting_invite:
                    case GameStatus.waiting_join:
                        return new Result { ResultFunc = this.BadRequest, Data = "This game hasn't started yet." };

                    case GameStatus.finished:
                        return new Result { ResultFunc = this.BadRequest, Data = "This game has been finished." };
                }
            }

            //Check if it's his turn.
            if (user != this.DecideTurn(game))
            {
                return new Result { ResultFunc = this.BadRequest, Data = "It's not your turn." };
            }

            //Check if turn is valid(Position not already chosen).
            if (CheckPosition(game, move.position) == false)
            {
                return new Result { ResultFunc = this.BadRequest, Data = "Position already chosen." };
            }

            int moves = this.db.Moves.Where(x => x.game_id == game.id).ToList().Count;
            //If move_count == 8 then this means this is the last set. So the game has been finished and it's a tie.
            int move_count = moves + 1;
            if (move_count == 8)
            {
                game.status = GameStatus.finished;
            }

            this.db.Add(new Move
            {
                user_id = user.id,
                game_id = game.id,
                position = move.position,
                move_count = move_count
            });
            this.db.SaveChanges();

            //Check if it's a winning move.
            User winner;
            if ((winner = this.CheckGameForWin(game)) != null)
            {
                game.status = GameStatus.finished;
                game.finished_at = DateTime.Now;
                game.UserWinner = winner;
                this.db.SaveChanges();
            }

            return new Result { ResultFunc = this.Ok, Data = this.FilterGameResults(game) };
        }
        #endregion


        #region "Read functions"
        private List<object> UserInvites()
        {
            User user = (User)HttpContext.Items["user"];
            List<Game> games = this.db.Games.Where(x => x.User2 == user && x.status == GameStatus.waiting_invite).Include(x => x.User1).ToList();

            return FilterGameResults(games);
        }

        private List<object> UserGamesStarted()
        {
            User user = (User)HttpContext.Items["user"];
            List<Game> games = this.db.Games.Where(x => (x.User1 == user || x.User2 == user) && x.status == GameStatus.started).Include(x => x.User1).Include(x => x.User2).Include(x => x.Moves).ToList();

            return FilterGameResults(games);
        }

        private List<object> UserGamesFinished()
        {
            User user = (User)HttpContext.Items["user"];
            List<Game> games = this.db.Games.Where(x => (x.User1 == user || x.User2 == user) && x.status == GameStatus.finished).Include(x => x.User1).Include(x => x.User2).Include(x => x.Moves).Include(x => x.UserWinner).ToList();

            return FilterGameResults(games);
        }

        private object UserStats()
        {
            User user = (User)HttpContext.Items["user"];
            int loses = this.db.Games.Count(x => (x.User1 == user || x.User2 == user) && x.UserWinner != user && x.status == GameStatus.finished);
            int wins = this.db.Games.Count(x => x.UserWinner == user && x.status == GameStatus.finished);
            int total_games_played = this.db.Games.Count(x => (x.User1 == user || x.User2 == user) && x.status == GameStatus.finished);

            return new
            {
                wins = wins,
                loses = loses,
                total_games_played = total_games_played
            };
        }

        private List<object> GameLobbyList()
        {
            User user = (User)HttpContext.Items["user"];
            List<Game> games = this.db.Games.Where(x => x.status == GameStatus.waiting_join && x.User1 != user).Include(x => x.User1).Include(x => x.User2).ToList();

            return FilterGameResults(games);
        }

        private Result GameInfo(int id)
        {
            User user = (User)HttpContext.Items["user"];

            //Check if game exists.
            Game game = this.db.Games.Where(x => x.id == id).Include(x => x.User1).Include(x => x.User2).Include(x => x.Moves).Include(x => x.UserWinner).FirstOrDefault();
            if (game == null)
            {
                return new Result { ResultFunc = this.BadRequest, Data = "Game does not exist." };
            }

            //Check if the user is a player of this game.
            if (game.User1 != user && game.User2 != user)
            {
                return new Result { ResultFunc = this.BadRequest, Data = "This is not your game." };
            }

            return new Result { ResultFunc = this.BadRequest, Data = FilterGameResults(game) };
        }
        #endregion


        #region "Functions that filter certain properties from the game Model"
        private List<object> FilterGameResults(List<Game> games)
        {
            List<object> result = new List<object>();
            foreach (Game game in games)
            {
                result.Add(FilterGameResults(game));
            }
            return result;
        }

        private object FilterGameResults(Game game)
        {
            return new
            {
                id = game.id,
                user1 = new
                {
                    id = game.User1.id,
                    name = game.User1.name
                },
                user2 = new
                {
                    id = game.User2?.id,
                    name = game.User2?.name
                },
                user_winner = new
                {
                    id = game.UserWinner?.id,
                    name = game.UserWinner?.name
                },
                moves = game.Moves,
                status = game.status,
                created_at = game.created_at,
                finished_at = game.finished_at
            };
        }
        #endregion


        public class Result
        {
            public Func<object, IActionResult> ResultFunc { get; set; }
            public object Data { get; set; }
        }
    }
}