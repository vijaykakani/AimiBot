﻿using Discord.Commands;
using NadekoBot.Attributes;
using NadekoBot.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Utility
{
    public partial class Utility
    {
        [Group]
        public class CalcCommands : ModuleBase
        {
            [NadekoCommand, Usage, Description, Aliases]
            public async Task Calculate([Remainder] string expression)
            {
                var expr = new NCalc.Expression(expression, NCalc.EvaluateOptions.IgnoreCase);
                expr.EvaluateParameter += Expr_EvaluateParameter;
                var result = expr.Evaluate();
                if (expr.Error == null)
                    await Context.Channel.SendConfirmAsync("Result", $"{result}");
                else
                    await Context.Channel.SendErrorAsync($"⚙ Error", expr.Error);
            }

            private static void Expr_EvaluateParameter(string name, NCalc.ParameterArgs args)
            {
                switch (name.ToLowerInvariant())
                {
                    case "pi":
                        args.Result = Math.PI;
                        break;
                    case "e":
                        args.Result = Math.E;
                        break;
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task CalcOps()
            {
                var selection = typeof(Math).GetTypeInfo().GetMethods().Except(typeof(object).GetTypeInfo().GetMethods()).Distinct(new MethodInfoEqualityComparer()).Select(x =>
                {
                    return x.Name;
                })
                .Except(new[] { "ToString",
                            "Equals",
                            "GetHashCode",
                            "GetType"});
                await Context.Channel.SendConfirmAsync("Available functions in calc", string.Join(", ", selection));
            }
        }

        class MethodInfoEqualityComparer : IEqualityComparer<MethodInfo>
        {
            public bool Equals(MethodInfo x, MethodInfo y) => x.Name == y.Name;

            public int GetHashCode(MethodInfo obj) => obj.Name.GetHashCode();
        }

        class ExpressionContext
        {
            public double Pi { get; set; } = Math.PI;
        }

    }
}