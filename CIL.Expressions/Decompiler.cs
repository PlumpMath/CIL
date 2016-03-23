﻿using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CIL.Expressions
{
    public static class Decompiler
    {
        public static Expression<T> GetExpression<T>(this T func)
            where T : class
        {
            if (func == null) throw new ArgumentNullException("func");
            var f = func as Delegate;
            var eval = new Stack<Expression>();
            var il = f.Method.GetInstructions();
            var args = f.Method.GetParameters()
                    .Select(x => Expression.Parameter(x.ParameterType, x.Name))
                    .ToArray();
            Process(il, args, eval);
            Debug.Assert(eval.Count == 1);
            Debug.Assert(false == il.MoveNext());
            // bools don't actually exist at CIL level, they are simply native ints
            // so they need special handling to decompile to a typed expression
            var e = eval.Pop();
            var body = f.Method.ReturnType != typeof(bool)  ? e:
                       e.NodeType == ExpressionType.Constant? Expression.Constant(1 == (int)(e as ConstantExpression).Value, f.Method.ReturnType):
                                                              Expression.Equal(e, Expression.Constant(1)) as Expression;
            Debug.Assert(body.Type == f.Method.ReturnType);
            return Expression.Lambda<T>(body, args);
        }

        static void Process(ILReader il, ParameterExpression[] args, Stack<Expression> eval)
        {
            //FIXME: could make the output even nicer by transforming op_* methods back into their respective
            //expressions, ie. op_Addition => Expression.Add, etc.
            //FIXME: currently missing try/catch-filter/finally.
            Expression rhs;
            while (il.MoveNext())
            {
                var x = il.Current.Simplify();
                switch (x.OpCode.Type())
                {
                    case OpType.Add_ovf:
                    case OpType.Add_ovf_un:
                        rhs = eval.Pop();
                        eval.Push(Expression.Add(eval.Pop(), rhs));
                        break;
                    case OpType.Add:
                        rhs = eval.Pop();
                        eval.Push(Expression.AddChecked(eval.Pop(), rhs));
                        break;
                    case OpType.And:
                        rhs = eval.Pop();
                        eval.Push(Expression.And(eval.Pop(), rhs));
                        break;
                    case OpType.Box:
                        //FIXME: I don't think I need to do anything, since the top of the stack is already the right type
                        break;
                    case OpType.Call:
                    case OpType.Callvirt:
                        var method = (MethodInfo)x.ResolveMethod();
                        var mparams = method.GetParameters();
                        var margs = mparams.Select(a => eval.Pop()).Reverse().ToArray();
                        var minstance = method.IsStatic ? null : eval.Pop();
                        eval.Push(Expression.Call(minstance, method, margs));
                        break;
                    case OpType.Castclass:
                        //eval.Push(Expression.TypeAs(eval.Pop(), x.ResolveType()));
                        eval.Push(Expression.Convert(eval.Pop(), x.ResolveType()));
                        break;
                    case OpType.Ceq:
                        rhs = eval.Pop();
                        eval.Push(Expression.Equal(eval.Pop(), rhs));
                        break;
                    case OpType.Cgt:
                    case OpType.Cgt_un:
                        rhs = eval.Pop();
                        eval.Push(Expression.GreaterThan(eval.Pop(), rhs));
                        break;
                    case OpType.Clt:
                    case OpType.Clt_un:
                        rhs = eval.Pop();
                        eval.Push(Expression.LessThan(eval.Pop(), rhs));
                        break;
                    case OpType.Constrained_:
                        break;
                    case OpType.Conv_i:
                        eval.Push(Expression.Convert(eval.Pop(), typeof(IntPtr)));
                        break;
                    case OpType.Conv_i1:
                        eval.Push(Expression.Convert(eval.Pop(), typeof(sbyte)));
                        break;
                    case OpType.Conv_i2:
                        eval.Push(Expression.Convert(eval.Pop(), typeof(short)));
                        break;
                    case OpType.Conv_i4:
                        eval.Push(Expression.Convert(eval.Pop(), typeof(int)));
                        break;
                    case OpType.Conv_i8:
                        eval.Push(Expression.Convert(eval.Pop(), typeof(long)));
                        break;
                    case OpType.Conv_ovf_i:
                    case OpType.Conv_ovf_i_un:
                        eval.Push(Expression.ConvertChecked(eval.Pop(), typeof(IntPtr)));
                        break;
                    case OpType.Conv_ovf_i1:
                    case OpType.Conv_ovf_i1_un:
                        eval.Push(Expression.ConvertChecked(eval.Pop(), typeof(sbyte)));
                        break;
                    case OpType.Conv_ovf_i2:
                    case OpType.Conv_ovf_i2_un:
                        eval.Push(Expression.ConvertChecked(eval.Pop(), typeof(short)));
                        break;
                    case OpType.Conv_ovf_i4:
                    case OpType.Conv_ovf_i4_un:
                        eval.Push(Expression.ConvertChecked(eval.Pop(), typeof(int)));
                        break;
                    case OpType.Conv_ovf_i8:
                    case OpType.Conv_ovf_i8_un:
                        eval.Push(Expression.ConvertChecked(eval.Pop(), typeof(long)));
                        break;
                    case OpType.Conv_r4:
                    case OpType.Conv_r_un:
                        eval.Push(Expression.ConvertChecked(eval.Pop(), typeof(float)));
                        break;
                    case OpType.Conv_r8:
                        eval.Push(Expression.ConvertChecked(eval.Pop(), typeof(double)));
                        break;
                    case OpType.Conv_u:
                        eval.Push(Expression.ConvertChecked(eval.Pop(), typeof(UIntPtr)));
                        break;
                    case OpType.Conv_u1:
                        eval.Push(Expression.ConvertChecked(eval.Pop(), typeof(byte)));
                        break;
                    case OpType.Conv_u2:
                        eval.Push(Expression.ConvertChecked(eval.Pop(), typeof(ushort)));
                        break;
                    case OpType.Conv_u4:
                        eval.Push(Expression.ConvertChecked(eval.Pop(), typeof(uint)));
                        break;
                    case OpType.Conv_u8:
                        eval.Push(Expression.ConvertChecked(eval.Pop(), typeof(ulong)));
                        break;
                    //FIXME: should this perhaps invoke Buffer.BlockCopy?
                    //case OpType.Cpblk:
                    //    break;
                    //case OpType.Cpobj:
                    //    break;
                    case OpType.Div:
                    case OpType.Div_un:
                        rhs = eval.Pop();
                        eval.Push(Expression.Divide(eval.Pop(), rhs));
                        break;
                    case OpType.Dup:
                        eval.Push(eval.Peek());
                        break;
                    //case OpType.Endfilter:
                    //    break;
                    //case OpType.Endfinally:
                    //    break;
                    //FIXME: should this perhaps invoke Buffer.BlockCopy?
                    //case OpType.Initblk:
                    //    break;
                    //case OpType.Initobj:
                    //    break;
                    case OpType.Isinst:
                        eval.Push(Expression.TypeAs(eval.Pop(), x.ResolveType()));
                        break;
                    case OpType.Ldarg:
                        eval.Push(args[x.Operand.Int32]);
                        break;
                    case OpType.Ldc_i4:
                        var i4 = x.Operand.Int32;
                        eval.Push(Expression.Constant(i4));
                        break;
                    case OpType.Ldc_i8:
                        var i8 = x.Operand.Int64;
                        eval.Push(Expression.Constant(i8));
                        break;
                    case OpType.Ldc_r8:
                        var r8 = x.Operand.Float64;
                        eval.Push(Expression.Constant(r8));
                        break;
                    case OpType.Ldc_r4:
                        var r4 = x.Operand.Float32;
                        eval.Push(Expression.Constant(r4));
                        break;
                    case OpType.Ldelem:
                    case OpType.Ldelem_i:
                    case OpType.Ldelem_i1:
                    case OpType.Ldelem_i2:
                    case OpType.Ldelem_i4:
                    case OpType.Ldelem_i8:
                    case OpType.Ldelem_r4:
                    case OpType.Ldelem_r8:
                    case OpType.Ldelem_u1:
                    case OpType.Ldelem_u2:
                    case OpType.Ldelem_u4:
                        rhs = eval.Pop();
                        eval.Push(Expression.ArrayIndex(eval.Pop(), rhs));
                        break;
                    case OpType.Ldfld:
                    case OpType.Ldsfld:
                        var field = x.ResolveField();
                        eval.Push(Expression.Field(field.IsStatic ? null : eval.Pop(), field));
                        return;
                    case OpType.Ldlen:
                        eval.Push(Expression.ArrayLength(eval.Pop()));
                        break;
                    case OpType.Ldloc:
                        var loc = il.Local(x.Operand.Int32);
                        eval.Push(Expression.Variable(loc.LocalType, "loc" + loc.LocalIndex));
                        break;
                    case OpType.Ldnull:
                        eval.Push(Expression.Constant(null));
                        break;
                    case OpType.Ldstr:
                        eval.Push(Expression.Constant(x.ResolveString()));
                        break;
                    case OpType.Ldtoken:
                        eval.Push(Expression.Constant(x.Resolve()));
                        break;
                    case OpType.Mul:
                        rhs = eval.Pop();
                        eval.Push(Expression.Multiply(eval.Pop(), rhs));
                        break;
                    case OpType.Mul_ovf:
                    case OpType.Mul_ovf_un:
                        rhs = eval.Pop();
                        eval.Push(Expression.MultiplyChecked(eval.Pop(), rhs));
                        break;
                    case OpType.Neg:
                        eval.Push(Expression.Negate(eval.Pop()));
                        break;
                    case OpType.Newarr:
                        var atype = x.ResolveType();
                        eval.Push(Expression.NewArrayBounds(atype, eval.Pop()));
                        break;
                    case OpType.Newobj:
                        var ctor = (ConstructorInfo)x.ResolveMethod();
                        var cparams = ctor.GetParameters();
                        var cargs = cparams.Select(a => eval.Pop()).Reverse().ToArray();
                        eval.Push(Expression.New(ctor, cargs));
                        break;
                    case OpType.Nop:
                        break;
                    case OpType.Not:
                        eval.Push(Expression.Not(eval.Pop()));
                        break;
                    case OpType.Or:
                        rhs = eval.Pop();
                        eval.Push(Expression.Or(eval.Pop(), rhs));
                        break;
                    case OpType.Pop:
                        eval.Pop();
                        break;
                    case OpType.Rem:
                    case OpType.Rem_un:
                        rhs = eval.Pop();
                        eval.Push(Expression.Modulo(eval.Pop(), rhs));
                        break;
                    case OpType.Ret:
                        return; //FIXME: is this correct?
                        //eval.Push(Expression.Return(eval.Pop()));
                        //break;
                    case OpType.Rethrow:
                        eval.Push(Expression.Rethrow());
                        break;
                    case OpType.Shl:
                        rhs = eval.Pop();
                        eval.Push(Expression.LeftShift(eval.Pop(), rhs));
                        break;
                    case OpType.Shr:
                    case OpType.Shr_un:
                        rhs = eval.Pop();
                        eval.Push(Expression.LeftShift(eval.Pop(), rhs));
                        break;
                    case OpType.Sizeof:
                        eval.Push(Expression.Constant(Marshal.SizeOf(x.ResolveType())));
                        break;
                    case OpType.Sub:
                        rhs = eval.Pop();
                        eval.Push(Expression.Subtract(eval.Pop(), rhs));
                        break;
                    case OpType.Sub_ovf:
                    case OpType.Sub_ovf_un:
                        rhs = eval.Pop();
                        eval.Push(Expression.SubtractChecked(eval.Pop(), rhs));
                        break;
                    case OpType.Switch:
                        //FIXME: this needs to recursively process
                        //eval.Push(Expression.Switch(eval.Pop(), ));
                        break;
                    case OpType.Tail_:
                        break;
                    case OpType.Throw:
                        eval.Push(Expression.Throw(eval.Pop()));
                        break;
                    case OpType.Unaligned_:
                        break;
                    case OpType.Unbox:
                    case OpType.Unbox_any:
                        eval.Push(Expression.Unbox(eval.Pop(), x.ResolveType()));
                        break;
                    default:
                        throw new ArgumentException("Can't translate CIL to Expression: " + x.ToString(), "instr");
                }
            }
        }
    }
}