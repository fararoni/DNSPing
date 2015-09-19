using JHSoftware;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DNSPing
{
    class Program
    {
        static String bicacora = "PingDns." + string.Format("{0:yy.M.d HH.mm.ss}", DateTime.Now) + ".csv";
        static int delay = 1000;
        static void Main(string[] args)
        {

            String[] listaRegistros = null;
            string[] listaDns = null;


            /// --
            Console.WriteLine("DnsPing 2015.03.03.02.30.fararoni at gmail.com");
            Console.WriteLine("Bitácora: " + @bicacora);
            Console.WriteLine("<CTRL><C> Para Salir ");
            if (args.Length < 2)
            {
                Console.WriteLine("Uso: dnsping <segundos> <registros a buscar separados por espacio> ");
                System.Console.ReadKey();
                return;
            }
            // Parámetros por consola
            try
            {
                delay = Convert.ToInt32(args[0]);
                delay *= 1000;
                Console.WriteLine("Segundos de espera en cada repetición: " + (delay / 1000));
                if (delay < 0)
                    return;

            }
            catch
            {
                Console.WriteLine("Uso: <segundos>: Número de segundo de espera en cada ciclo.");
                System.Console.ReadKey();

                return;
            }

            try
            {
                listaRegistros = new String[args.Length - 1];
                for (int i = 0; i < (args.Length - 1); i++)
                {
                    listaRegistros[i] = args[i + 1];
                }
            }
            catch
            {
                Console.WriteLine("Uso: <registro a buscar>: registro a buscar ej. www.google.com www.gnu.org");
                System.Console.ReadKey();

                return;
            }


            /// Parametros por el archivo

            try
            {
                listaDns = System.IO.File.ReadAllLines(@"DnsPing.listadns.txt");

            }
            catch
            {
                Console.WriteLine("Error al leer el archivo: 'DnsPing.listadns.txt' ; en este archivo se deben listar los nombres de los servidores de nombres a usar. Ej. ns1.example.com");
                System.Console.ReadKey();
               
                return;
            }

            //---
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(@bicacora, true))
            {

                file.WriteLine("Lista de Registros: "); foreach (string registro in listaRegistros) file.WriteLine(registro);
                file.WriteLine("Lista de DNS's: "); foreach (string dns in listaDns) file.WriteLine(dns);
                file.WriteLine("Segundos de espera en cada repetición: " + delay);

            }
            //---

            System.Console.WriteLine("\n" + DateTime.Now);
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(@bicacora, true))
            {
                Registro r = new Registro();
                file.WriteLine(r.HeaderLog());
            }


            Thread[,] threadArrayDns = new Thread[listaDns.Length, listaRegistros.Length];

            for (int i = 0; i < listaDns.Length; i++)
            {
                for (int j = 0; j < listaRegistros.Length; j++)
                {
                    ResolveRecord rs = new ResolveRecord();

                    System.Console.WriteLine(String.Format("DNS {0,2} {1,-31} {2}", i, listaDns[i], listaRegistros[j]));
                    threadArrayDns[i, j] = new Thread(rs.resolve);
                    rs.bicacora = bicacora;
                    rs.dns = listaDns[i];
                    rs.registro = listaRegistros[j];
                    rs.delay = delay;

                    threadArrayDns[i, j].Start();
                }
            }


            for (int i = 0; i < listaDns.Length; i++)
            {
                for (int j = 0; i < listaRegistros.Length; j++)
                {
                    threadArrayDns[i, j].Join();
                }
            }

            Console.WriteLine("Terminado");

            System.Console.ReadKey();

        } // Fin Main



    } // Fin Class Program

    class ResolveRecord
    {

        JHSoftware.DnsClient.RequestOptions OptionsDns;

        public String bicacora { get; set; }
        public String dns { get; set; }
        public String registro { get; set; }
        public int delay { get; set; }


        public ResolveRecord()
        {
            OptionsDns = new JHSoftware.DnsClient.RequestOptions();
            OptionsDns.DnsServers = new System.Net.IPAddress[] { System.Net.IPAddress.Parse("8.8.8.8"), System.Net.IPAddress.Parse("8.8.4.4") };

        }
        public void resolve()
        {
            long cont = 0;

            while (true)
            {
                cont++;


                Registro r = resolve(dns, registro);
                System.Console.WriteLine(string.Format("{0:MMdd HH:mm:ss} {1,5} {2}", DateTime.Now, cont, r));


                _readWriteLock.EnterWriteLock();
                try
                {
                    using (StreamWriter file = new StreamWriter(@bicacora, true))
                    {
                        file.WriteLine(r.ToStringLog());

                        file.Close();
                    }
                }
                catch
                {
                    System.Console.WriteLine("Error al guardar la bitácora");
                }
                _readWriteLock.ExitWriteLock();

                Thread.Sleep(delay);
            }
        }
        private static ReaderWriterLockSlim _readWriteLock = new ReaderWriterLockSlim();


        public Registro resolve(String dns, String registro)
        {
            Registro r = new Registro();
            r.NameServer = dns;
            r.Record = registro;
            r.IpNameServer = "?.?.?.?";
            //1.- Resolver la IP del Servidor de DNS
            try
            {
                IPAddress address = IPAddress.Parse(r.NameServer);
                r.IpNameServer = r.NameServer;
                r.NameServer = "";
            }
            catch (Exception)
            {
                ////--- Nada que hacer
            }

            if ("?.?.?.?".Equals(r.IpNameServer))
            {
                try
                {
                    r.FechaHora = string.Format("{0}", DateTime.Now);
                    var IPs = JHSoftware.DnsClient.LookupHost(dns, JHSoftware.DnsClient.IPVersion.IPv4, OptionsDns);
                    foreach (var IP in IPs)
                    {
                        r.IpNameServer = IP.ToString();
                        break;
                    }
                }
                catch (DnsClient.NoDefinitiveAnswerException ndef)
                {
                    foreach (JHSoftware.DnsClient.NoDefinitiveAnswerException.ServerProblem sp in ndef.ServerProblems)
                        r.MensajeError.Append(string.Format("NS+ {0}", sp.ProblemDescription));
                    return r;
                }
                catch (Exception e)
                {
                    r.MensajeError.Append(string.Format("NS- {0}", e.Message));
                    return r;
                }
            }
            //2.- Resolver la IP del RegistroDnsBuscado
            DateTime start = DateTime.Now;
            r.FechaHora = string.Format("{0}", start);
            try
            {

                var Options = new JHSoftware.DnsClient.RequestOptions();
                Options.DnsServers = new System.Net.IPAddress[] { System.Net.IPAddress.Parse(r.IpNameServer) };
                var Response = JHSoftware.DnsClient.Lookup(r.Record, JHSoftware.DnsClient.RecordType.A, Options);
                TimeSpan timeDiff = DateTime.Now - start;

                r.IpNameServer = string.Format("{0}", Response.FromServer);
                r.TiempoResolucion = string.Format("{0,7:0.00}", timeDiff.TotalMilliseconds);
                r.Autoritativo = (Response.AuthoritativeAnswer ? "Autoritativa" : "No Autoritativa");
                r.Recursivo = (Response.RecursionAvailable ? "Recursivo" : "No Recursivo");

                foreach (var Record in Response.AnswerRecords)
                {
                    r.RegistroA.Append(string.Format("{0} {1} {2,3} {3,15}",
                                            Record.Name,
                                            Record.Type.ToString(),
                                            Record.TTL,
                                            Record.Data));
                }

            }
            catch (DnsClient.NoDefinitiveAnswerException ndef)
            {
                TimeSpan timeDiff = DateTime.Now - start;
                r.TiempoResolucion = string.Format("{0,7:0.00}", timeDiff.TotalMilliseconds);
                foreach (JHSoftware.DnsClient.NoDefinitiveAnswerException.ServerProblem sp in ndef.ServerProblems)
                    r.MensajeError.Append(sp.ProblemDescription);

            }
            catch (DnsClient.NXDomainException ex)
            {
                TimeSpan timeDiff = DateTime.Now - start;
                r.TiempoResolucion = string.Format("{0,7:0.00}", timeDiff.TotalMilliseconds);
                r.MensajeError.Append(ex.Message);

            }
            catch (JHSoftware.DnsClient.NoDataException ex)
            {
                TimeSpan timeDiff = DateTime.Now - start;
                r.TiempoResolucion = string.Format("{0,7:0.00}", timeDiff.TotalMilliseconds);
                r.MensajeError.Append(ex.Message);
            }

            catch (Exception e)
            {
                TimeSpan timeDiff = DateTime.Now - start;
                r.TiempoResolucion = string.Format("{0,7:0.00}", timeDiff.TotalMilliseconds);

                r.MensajeError.Append(e.Message);
            }
            //System.Console.WriteLine(r);
            return r;
        } // Fin Resolve


    }

    class Registro
    {
        public Registro()
        {
            RegistroA = new StringBuilder();
            MensajeError = new StringBuilder();
        }
        public String FechaHora { get; set; }
        public String NameServer { get; set; }
        public String IpNameServer { get; set; }
        public String Record { get; set; }
        public String IpsRecord { get; set; }
        public String Recursivo { get; set; }
        public String Autoritativo { get; set; }
        public StringBuilder RegistroA { get; set; }
        public String TiempoResolucion { get; set; }
        public StringBuilder MensajeError { get; set; }


        public override string ToString()
        {
            String s = null;
            if (("".Equals(Autoritativo) || Autoritativo == null) && ("".Equals(Recursivo) || Recursivo == null))
                s = string.Format("{0,9:6.2}*{1,-31}*{2} Err: {3} ", TiempoResolucion, ("".Equals(NameServer) ? IpNameServer : NameServer), Record, MensajeError);
            else
                s = string.Format("{0,9:6.2} {1,-31} {2,15} {3,12} {4} {5}", TiempoResolucion, ("".Equals(NameServer) ? IpNameServer : NameServer), Autoritativo, Recursivo, RegistroA, MensajeError);
            return s;
        }
        public string HeaderLog()
        {
            String s = "" + "FechaHora"
                          + ",[," + "NameServer"
                          + "," + "IpNameServer"
                          + "," + "Autoridad"
                          + "," + "Recursividad"
                          + ",][," + "Registro"
                          + ",][," + "Registro A"
                          + ",]," + "Tiempo Resolucion"
                          + "," + "Mensaje";
            return s;
        }

        public string ToStringLog()
        {
            String s = "" + FechaHora
                          + ",[," + NameServer
                          + "," + IpNameServer
                          + "," + Autoritativo
                          + "," + Recursivo
                          + ",][," + Record
                //+","+ IpsRecord
                          + ",][," + RegistroA
                          + ",]," + TiempoResolucion
                          + "," + MensajeError;
            return s;
        }
    }

    class Html
    {
        public static String code(string Url)
        {
            String msg = "";
            try
            {
                HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(Url);
                myRequest.Method = "GET";
                WebResponse myResponse = myRequest.GetResponse();
                StreamReader sr = new StreamReader(myResponse.GetResponseStream(), System.Text.Encoding.UTF8);
                msg = sr.ReadToEnd();
                sr.Close();
                myResponse.Close();
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.Message);
            }
            return msg;
        }
    }
}