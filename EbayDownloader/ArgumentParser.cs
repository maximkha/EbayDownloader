using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandParser
{
    public static class EX_ArgumentParser
    {
        public static Dictionary<A, C> ApplyToKey<A, B, C>(this Dictionary<A, B> orig, Func<B, C> transform)
        {
            Dictionary<A, C> res = new Dictionary<A, C>();
            foreach (KeyValuePair<A, B> keyValuePair in orig)
            {
                res.Add(keyValuePair.Key, transform(keyValuePair.Value));
            }
            return res;
        }

        public static int IndexOf<T>(this List<T> list, Func<T, bool> selector)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (selector(list[i])) return i;
            }
            return -1;
        }
    }

    public class ArgumentParser
    {
        public enum ArgumentFormat
        {
            Linear,
            KVP
        }

        public enum ArgumentType
        {
            None,
            Int,
            Double,
            String,
        }

        public struct ParsedArguments
        {
            public Dictionary<string, int[]> IntArguments;
            public Dictionary<string, double[]> DoubleArguments;
            public Dictionary<string, string[]> StringArguments;
        }

        ArgumentFormat Format;
        List<Tuple<string, bool, bool, ArgumentType>> ArgumentTypes = new List<Tuple<string, bool, bool, ArgumentType>>();
        bool CaseInsensitiveKey = false;
        public Action Help = null;
        public string InvokeHelp = "help";
        public bool HelpIfEmpty = true;

        //TODO: implement gluing
        //This means a parameter e.g. "t" can be invoked by '-t""', notice that there is no space between the parameter name and value
        //public bool AllowGluing = false;

        public ArgumentParser(ArgumentFormat argumentFormat, bool caseInsensitiveKey)
        {
            Format = argumentFormat;
            CaseInsensitiveKey = caseInsensitiveKey;
        }

        public void AddArgument(string key, ArgumentType argumentType, bool optional = false, bool allowCasting = true)
        {
            //if (Format == ArgumentFormat.Linear && optional) throw new Exception("Linear arguments cannot be optional");
            //Ending arguments can be optional
            ArgumentTypes.Add(new Tuple<string, bool, bool, ArgumentType>(CaseInsensitiveKey ? key.ToLower() : key, optional, allowCasting, argumentType));
        }

        public static bool isLinear(string[] arguments)
        {
            //if (Format == ArgumentFormat.Linear) return true;
            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].Length == 0) continue;
                if (arguments[i][0] == '-')
                {
                    int intArg = 0;
                    double doubleArg = double.NaN;
                    bool negNumeric = false;
                    bool parsed = double.TryParse(arguments[i], out doubleArg);
                    negNumeric = parsed && doubleArg < 0;
                    parsed = int.TryParse(arguments[i], out intArg);
                    negNumeric = parsed && intArg < 0;
                    if (negNumeric) continue;
                    return false;
                }
            }
            return true;
        }

        public ParsedArguments Parse(string[] arguments)
        {
            ParsedArguments parsedArguments = new ParsedArguments();
            Dictionary<string, List<int>> intArguments = new Dictionary<string, List<int>>();
            Dictionary<string, List<double>> doubleArguments = new Dictionary<string, List<double>>();
            Dictionary<string, List<string>> stringArguments = new Dictionary<string, List<string>>();

            if (Format == ArgumentFormat.KVP)
            {
                string key = string.Empty;
                ArgumentType expArgumentType = ArgumentType.None;
                bool allowCasting = false;

                for (int i = 0; i < arguments.Length; i++)
                {
                    string argument = arguments[i];

                    ArgumentType foundArgumentType = ArgumentType.String;

                    int intArg = 0;
                    double doubleArg = double.NaN;
                    bool negNumeric = false;
                    bool parsed = double.TryParse(argument, out doubleArg);
                    if (parsed) foundArgumentType = ArgumentType.Double;
                    negNumeric = parsed && doubleArg < 0;
                    parsed = int.TryParse(argument, out intArg);
                    if (parsed) foundArgumentType = ArgumentType.Int;
                    negNumeric = parsed && intArg < 0;
                    //if (foundArgumentType == ArgumentType.None) foundArgumentType = ArgumentType.String; //Assume it's a string

                    if (argument.Length == 0) continue;
                    if (argument[0] == '-' && !negNumeric)
                    {
                        //this is a key
                        key = argument.Substring(1);
                        if (key.Length == 0) continue;
                        if (key.ToLower() == InvokeHelp)
                        {
                            Help();
                            throw new Exception("");
                        }
                        if (CaseInsensitiveKey) key = key.ToLower();
                        if (!ArgumentTypes.Any((x) => x.Item1 == key))
                        {
                            //No matching argument name
                            throw new Exception("Invalid argument named '" + argument + "'");
                        }
                        Tuple<string, bool, bool, ArgumentType> argumentInfo = ArgumentTypes.Where((x) => x.Item1 == key).ToArray()[0];
                        expArgumentType = argumentInfo.Item4;
                        allowCasting = argumentInfo.Item3;
                        if (expArgumentType == ArgumentType.String) stringArguments.Add(key, new List<string>());
                        else if (expArgumentType == ArgumentType.Int) intArguments.Add(key, new List<int>());
                        else if (expArgumentType == ArgumentType.Double) doubleArguments.Add(key, new List<double>());
                        continue;
                    }
                    else
                    {
                        if (expArgumentType == ArgumentType.Double)
                        {
                            if (foundArgumentType == ArgumentType.Double)
                            {
                                doubleArguments[key].Add(double.Parse(argument));
                            }
                            else if (foundArgumentType == ArgumentType.Int)
                            {
                                if (allowCasting)
                                {
                                    doubleArguments[key].Add(int.Parse(argument));
                                }
                                else
                                {
                                    throw new Exception("Invalid value for " + key + " expected type: float");
                                }
                            }
                            else if (foundArgumentType == ArgumentType.String)
                            {
                                throw new Exception("Invalid value for " + key + " expected type: float");
                            }
                        }
                        else if (expArgumentType == ArgumentType.Int)
                        {
                            if (foundArgumentType == ArgumentType.Double)
                            {
                                if (allowCasting)
                                {
                                    intArguments[key].Add(Convert.ToInt32(double.Parse(argument)));
                                }
                                else
                                {
                                    throw new Exception("Invalid value for " + key + " expected type: int");
                                }
                            }
                            else if (foundArgumentType == ArgumentType.Int)
                            {
                                intArguments[key].Add(int.Parse(argument));
                            }
                            else if (foundArgumentType == ArgumentType.String)
                            {
                                throw new Exception("Invalid value for " + key + " expected type: int");
                            }
                        }
                        else if (expArgumentType == ArgumentType.String)
                        {
                            if (foundArgumentType == ArgumentType.Double)
                            {
                                if (allowCasting)
                                {
                                    stringArguments[key].Add(argument);
                                }
                                else
                                {
                                    throw new Exception("Invalid value for " + key + " expected type: string");
                                }
                            }
                            else if (foundArgumentType == ArgumentType.Int)
                            {
                                if (allowCasting)
                                {
                                    stringArguments[key].Add(argument);
                                }
                                else
                                {
                                    throw new Exception("Invalid value for " + key + " expected type: string");
                                }
                            }
                            else if (foundArgumentType == ArgumentType.String)
                            {
                                stringArguments[key].Add(argument);
                            }
                        }
                    }
                }

                if (intArguments.Count == 0 && stringArguments.Count == 0 && doubleArguments.Count == 0 && HelpIfEmpty)
                {
                    Help();
                    throw new Exception("");
                }

                for (int i = 0; i < ArgumentTypes.Count; i++)
                {
                    if (!ArgumentTypes[i].Item2)
                    {
                        //not Optional
                        bool pres = intArguments.ContainsKey(ArgumentTypes[i].Item1) || stringArguments.ContainsKey(ArgumentTypes[i].Item1) || doubleArguments.ContainsKey(ArgumentTypes[i].Item1);
                        if (!pres)
                        {
                            throw new Exception("Couldn't find the required argument " + ArgumentTypes[i].Item1);
                        }
                    }
                }
                parsedArguments.DoubleArguments = doubleArguments.ApplyToKey((x) => x.ToArray());
                parsedArguments.IntArguments = intArguments.ApplyToKey((x) => x.ToArray());
                parsedArguments.StringArguments = stringArguments.ApplyToKey((x) => x.ToArray());

                return parsedArguments;
            }
            else if (Format == ArgumentFormat.Linear)
            {
                //this is the minimum length of arguments
                for (int i = 0; i < arguments.Length; i++)
                {
                    if (i + 1 > ArgumentTypes.Count)
                    {
                        throw new Exception("Too many arguments");
                    }
                    string argument = arguments[i];

                    ArgumentType foundArgumentType = ArgumentType.String;

                    int intArg = 0;
                    double doubleArg = double.NaN;
                    bool parsed = double.TryParse(argument, out doubleArg);
                    if (parsed) foundArgumentType = ArgumentType.Double;
                    parsed = int.TryParse(argument, out intArg);
                    if (parsed) foundArgumentType = ArgumentType.Int;

                    string key = ArgumentTypes[i].Item1;
                    ArgumentType expArgumentType = ArgumentTypes[i].Item4;
                    bool allowCasting = ArgumentTypes[i].Item3;

                    if (expArgumentType == ArgumentType.String) stringArguments.Add(key, new List<string>());
                    else if (expArgumentType == ArgumentType.Int) intArguments.Add(key, new List<int>());
                    else if (expArgumentType == ArgumentType.Double) doubleArguments.Add(key, new List<double>());

                    if (expArgumentType == ArgumentType.Double)
                    {
                        if (foundArgumentType == ArgumentType.Double)
                        {
                            doubleArguments[key].Add(double.Parse(argument));
                        }
                        else if (foundArgumentType == ArgumentType.Int)
                        {
                            if (allowCasting)
                            {
                                doubleArguments[key].Add(int.Parse(argument));
                            }
                            else
                            {
                                throw new Exception("Invalid value for " + key + " expected type: float");
                            }
                        }
                        else if (foundArgumentType == ArgumentType.String)
                        {
                            throw new Exception("Invalid value for " + key + " expected type: float");
                        }
                    }
                    else if (expArgumentType == ArgumentType.Int)
                    {
                        if (foundArgumentType == ArgumentType.Double)
                        {
                            if (allowCasting)
                            {
                                intArguments[key].Add(Convert.ToInt32(double.Parse(argument)));
                            }
                            else
                            {
                                throw new Exception("Invalid value for " + key + " expected type: int");
                            }
                        }
                        else if (foundArgumentType == ArgumentType.Int)
                        {
                            intArguments[key].Add(int.Parse(argument));
                        }
                        else if (foundArgumentType == ArgumentType.String)
                        {
                            throw new Exception("Invalid value for " + key + " expected type: int");
                        }
                    }
                    else if (expArgumentType == ArgumentType.String)
                    {
                        if (foundArgumentType == ArgumentType.Double)
                        {
                            if (allowCasting)
                            {
                                stringArguments[key].Add(argument);
                            }
                            else
                            {
                                throw new Exception("Invalid value for " + key + " expected type: string");
                            }
                        }
                        else if (foundArgumentType == ArgumentType.Int)
                        {
                            if (allowCasting)
                            {
                                stringArguments[key].Add(argument);
                            }
                            else
                            {
                                throw new Exception("Invalid value for " + key + " expected type: string");
                            }
                        }
                        else if (foundArgumentType == ArgumentType.String)
                        {
                            stringArguments[key].Add(argument);
                        }
                    }
                }

                int optIndx = ArgumentTypes.IndexOf((x) => x.Item2);
                if (optIndx + 1 > arguments.Length)
                {
                    //Not enough arguments
                    throw new Exception("Not enough arguments!");
                }

                parsedArguments.DoubleArguments = doubleArguments.ApplyToKey((x) => x.ToArray());
                parsedArguments.IntArguments = intArguments.ApplyToKey((x) => x.ToArray());
                parsedArguments.StringArguments = stringArguments.ApplyToKey((x) => x.ToArray());

                return parsedArguments;
            }

            throw new Exception("Unset Format");
        }
    }
}