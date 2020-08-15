namespace Model

open Newtonsoft.Json

type Parent = 
    { FamilyName: string
      FirstName: string }

type Pet = 
    { GivenName: string }

type Child = 
    { FamilyName: string
      FirstName: string
      Gender: string
      Grade: int
      Pets: Pet[] }

type Address = 
    { State: string
      County: string
      City: string }

type Family = 
    { [< JsonProperty (PropertyName = "id") >]
      Id: string
      LastName: string
      Parents: Parent[]
      Children: Child[]
      Address: Address
      IsRegistered: bool }
    override __.ToString () = JsonConvert.SerializeObject __
