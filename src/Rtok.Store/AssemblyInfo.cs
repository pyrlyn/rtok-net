using Microsoft.EntityFrameworkCore.Infrastructure;
using Rtok.Store.Model;

// The context lives in Rtok.Store.Model; EF's query precompiler looks for it here.
[assembly: DbContext(typeof(RtokDbContext))]
