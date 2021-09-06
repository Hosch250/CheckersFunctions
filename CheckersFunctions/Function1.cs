using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static Checkers.Generic;
using System.Collections.Generic;
using Checkers;
using System.Threading;
using Microsoft.FSharp.Core;
using Microsoft.FSharp.Collections;

namespace CheckersFunctions
{
    public enum Variant
    {
        AmericanCheckers = 0,
        PoolCheckers = 1,
        AmericanCheckersOptionalJump = 2
    }
    public enum Player
    {
        Black = 0,
        White = 1
    }
    public enum PieceType
    {
        Checker = 0,
        King = 1
    }
    public class Coord
    {
        public int Row { get; set; }
        public int Column { get; set; }
    }
    public class PdnTurn
    {
        public int MoveNumber { get; set; }
        public PdnMove? BlackMove { get; set; }
        public PdnMove? WhiteMove { get; set; }
    }
    public class PdnMove
    {
        public List<int> Move { get; set; }
        public string ResultingFen { get; set; }
        public string DisplayString { get; set; }
        public PieceType? PieceTypeMoved { get; set; }
        public Player? Player { get; set; }
        public bool? IsJump { get; set; }
    }
    public class GameController
    {
        public Variant Variant { get; set; }
        public Player CurrentPlayer { get; set; }
        public Coord? CurrentCoord { get; set; }
        public string InitialPosition { get; set; }
        public List<PdnTurn> MoveHistory { get; set; }
        public Piece?[,] Board { get; set; }
    }
    public class Piece
    {
        public Player Player { get; set; }
        public PieceType PieceType { get; set; }
    }
    public static class Extensions
    {
        public static FSharpOption<Checkers.Piece.Piece> Map(this Piece piece)
        {
            return (piece.Player, piece.PieceType) switch
            {
                (Player.White, PieceType.Checker) => Checkers.Piece.whiteChecker,
                (Player.White, PieceType.King) => Checkers.Piece.whiteKing,
                (Player.Black, PieceType.Checker) => Checkers.Piece.blackChecker,
                (Player.Black, PieceType.King) => Checkers.Piece.blackKing,
            };
        }

        public static FSharpOption<Checkers.Piece.Piece>[,] Map(this Piece?[,] board)
        {
            var newBoard = Array2DModule.Create(board.GetLength(0), board.GetLength(1), FSharpOption<Checkers.Piece.Piece>.None);
            for (var i = 0; i < board.GetLength(0); i++)
            {
                for (var j = 0; j < board.GetLength(1); j++)
                {
                    newBoard[i, j] = board[i, j]?.Map();
                }
            }

            return newBoard;
        }

        public static FSharpList<Checkers.Generic.PdnTurn> Map(this List<PdnTurn> pdnTurns)
        {
            return ListModule.OfSeq(pdnTurns.Select(s => s.Map()));
        }

        public static Checkers.Generic.PdnTurn Map(this PdnTurn pdnTurn)
        {
            return new Generic.PdnTurn(pdnTurn.MoveNumber, pdnTurn.BlackMove?.Map(), pdnTurn.WhiteMove?.Map());
        }

        public static Checkers.Generic.PdnMove Map(this PdnMove pdnMove)
        {
            return new Generic.PdnMove(ListModule.OfSeq(pdnMove.Move), pdnMove.ResultingFen, pdnMove.DisplayString, pdnMove.PieceTypeMoved?.Map(), pdnMove.Player?.Map(), pdnMove.IsJump);
        }

        public static Checkers.Generic.PieceType Map(this PieceType pieceType)
        {
            return pieceType switch
            {
                PieceType.Checker => Checkers.Generic.PieceType.Checker,
                PieceType.King => Checkers.Generic.PieceType.King
            };
        }

        public static Checkers.Generic.Player Map(this Player player)
        {
            return player switch
            {
                Player.White => Checkers.Generic.Player.White,
                Player.Black => Checkers.Generic.Player.Black
            };
        }

        public static FSharpOption<Checkers.Generic.Coord> Map(this Coord coord)
        {
            return new FSharpOption<Generic.Coord>(new Generic.Coord(coord.Row, coord.Column));
        }
    }

    public static class CheckersAi
    {
        [FunctionName("CheckersAi_GetMove")]
        public static IActionResult GetMove(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "CheckersAi_GetMove/{depth}")] GameController controller,
            int depth,
            ILogger log)
        {
            log.LogInformation("CheckersAi.GetMove HTTP trigger function processed a request.");

            var variant = controller.Variant switch {
                Variant.AmericanCheckers => Checkers.GameVariant.GameVariant.AmericanCheckers,
                Variant.AmericanCheckersOptionalJump => Checkers.GameVariant.GameVariant.AmericanCheckersOptionalJump,
                Variant.PoolCheckers => Checkers.GameVariant.GameVariant.PoolCheckers
            };
            var fscontroller = new Checkers.GameController.GameController(variant, controller.Board.Map(), controller.CurrentPlayer.Map(), controller.InitialPosition, controller.MoveHistory.Map(), controller.CurrentCoord?.Map());

            var move = Checkers.PublicAPI.getMove(depth, fscontroller, CancellationToken.None);

            return new OkObjectResult(move);
        }
    }
}
